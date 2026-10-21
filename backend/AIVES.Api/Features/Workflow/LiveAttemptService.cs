using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AIVES.Api.Integrations;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Workflow;

public record LiveTurnGrant(DateTime Deadline, string Transcript);
public record LiveAttemptEvent(string Type, string TurnId, string Text = "", string? Transcript = null);

public sealed class LiveStreamLimits(IConfiguration config)
{
    private readonly ConcurrentDictionary<(int Exam, int Student), byte> active = new();
    private readonly SemaphoreSlim capacity = new(Math.Clamp(config.GetValue("Speech:MaxConcurrentStreams", 4), 1, 64));
    public IDisposable Enter(int exam, int student)
    {
        Require(capacity.Wait(0), "LiveCapacity: đã hết số luồng microphone đồng thời của backend; thử lại sau.", 429);
        if (!active.TryAdd((exam, student), 0)) { capacity.Release(); throw new WorkflowException(409, "LiveAlreadyOpen: phiên này đã có luồng microphone, hãy dừng luồng cũ."); }
        return new Lease(() => { active.TryRemove((exam, student), out _); capacity.Release(); });
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) release(); }
    }
}

public class LiveAttemptService(IServiceScopeFactory scopes, IGeminiLiveTranscriber transcriber, LiveStreamLimits limits, TimeProvider clock)
{
    public async IAsyncEnumerable<LiveAttemptEvent> Stream(int exam, int student, string turnId, ChannelReader<string> audio,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var grant = await InScope(e => e.AudioGrant(exam, student, turnId, ct));
        using var lease = limits.Enter(exam, student);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var remaining = grant.Deadline - clock.GetUtcNow().UtcDateTime;
        Require(remaining > TimeSpan.Zero, "LiveDeadline: lượt trả lời đã hết giờ.", 409);
        deadline.CancelAfter(remaining < TimeSpan.FromSeconds(540) ? remaining : TimeSpan.FromSeconds(540));
        var stopwatch = Stopwatch.StartNew();
        var lastCheck = TimeSpan.Zero;
        long bytes = 0;
        var session = Guid.NewGuid().ToString("N");
        var sequence = 0;
        await foreach (var item in transcriber.Stream(Frames(), deadline.Token))
        {
            Require(clock.GetUtcNow().UtcDateTime < grant.Deadline, "LiveDeadline: transcript đến sau deadline.", 409);
            if (item.Type == "final" && !string.IsNullOrWhiteSpace(item.Text))
            {
                var id = $"{session}:{++sequence}";
                var transcript = await InScope(e => e.PersistLiveTranscript(exam, student, turnId, id, item.Text, deadline.Token));
                yield return new("final", turnId, item.Text, transcript);
            }
            else yield return new(item.Type, turnId, item.Text, item.Type == "ready" ? grant.Transcript : null);
        }

        async IAsyncEnumerable<byte[]> Frames()
        {
            await foreach (var encoded in audio.ReadAllAsync(deadline.Token))
            {
                Require(encoded != null && encoded.Length is > 0 and <= 8536, "LiveAudioInvalid: frame base64 quá lớn.", 415);
                byte[] frame;
                try { frame = Convert.FromBase64String(encoded); }
                catch (FormatException) { throw new WorkflowException(415, "LiveAudioInvalid: base64 không hợp lệ."); }
                Require(frame.Length is > 0 and <= 6400 && frame.Length % 2 == 0, "LiveAudioInvalid: chỉ nhận PCM16 mono 16kHz tối đa 200ms/frame.", 415);
                bytes += frame.Length;
                Require(bytes <= (stopwatch.Elapsed.TotalSeconds + 2) * 32000, "LiveAudioRate: không gửi audio nhanh hơn thời gian thực.", 429);
                Require(clock.GetUtcNow().UtcDateTime < grant.Deadline, "LiveDeadline: lượt trả lời đã hết giờ.", 409);
                if (stopwatch.Elapsed - lastCheck >= TimeSpan.FromSeconds(1))
                {
                    await InScope(e => e.AudioGrant(exam, student, turnId, deadline.Token));
                    lastCheck = stopwatch.Elapsed;
                }
                yield return frame;
            }
        }
    }

    public async Task LogError(int exam, int student, string message)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<VivaEngine>();
            await engine.LogSpeechError(exam, student, message, CancellationToken.None);
        }
        catch { /* DB/network failure must not expose internal diagnostics or vendor key. */ }
    }
    private async Task<T> InScope<T>(Func<VivaEngine, Task<T>> operation)
    {
        using var scope = scopes.CreateScope(); return await operation(scope.ServiceProvider.GetRequiredService<VivaEngine>());
    }
}
