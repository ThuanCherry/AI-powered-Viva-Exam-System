using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AIVES.Api.Features.Workflow;

namespace AIVES.Api.Integrations;

public record LiveText(string Type, string Text = "");
public interface IGeminiLiveTranscriber
{
    IAsyncEnumerable<LiveText> Stream(IAsyncEnumerable<byte[]> pcm, CancellationToken ct);
}

// Injectable transport keeps protocol tests meaningful without sending student audio to a vendor.
public interface IGeminiSocket : IAsyncDisposable
{
    Task Connect(string key, CancellationToken ct);
    Task Send(string json, CancellationToken ct);
    Task<string?> Receive(CancellationToken ct);
}
public interface IGeminiSocketFactory { IGeminiSocket Create(); }
public class GeminiSocketFactory : IGeminiSocketFactory { public IGeminiSocket Create() => new GeminiSocket(); }

public sealed class GeminiSocket : IGeminiSocket
{
    private readonly ClientWebSocket socket = new();
    public async Task Connect(string key, CancellationToken ct)
    {
        // The official Google C#/Go clients use this header for backend WebSocket authentication.
        // The key never appears in a browser URL, query string or request log.
        socket.Options.SetRequestHeader("x-goog-api-key", key);
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        await socket.ConnectAsync(new Uri("wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent"), ct);
    }
    public Task Send(string json, CancellationToken ct) => socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    public async Task<string?> Receive(CancellationToken ct)
    {
        using var buffer = new MemoryStream(); var bytes = new byte[8192];
        WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(bytes, ct);
            if (frame.MessageType == WebSocketMessageType.Close)
            {
                if (socket.CloseStatus is WebSocketCloseStatus.NormalClosure) return null;
                throw new WorkflowException(502, $"GeminiLiveClosed: mã {(int?)socket.CloseStatus}; kiểm tra model, quota hoặc mạng.");
            }
            WorkflowRules.Require(frame.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary && buffer.Length + frame.Count <= 131072, "GeminiLiveInvalidResponse: frame sai hoặc quá lớn.", 502);
            buffer.Write(bytes, 0, frame.Count);
        } while (!frame.EndOfMessage);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
    public ValueTask DisposeAsync() { socket.Abort(); socket.Dispose(); return ValueTask.CompletedTask; }
}

public class GeminiLiveTranscriber(IConfiguration config, ProviderStatus status, IGeminiSocketFactory sockets) : IGeminiLiveTranscriber
{
    public async IAsyncEnumerable<LiveText> Stream(IAsyncEnumerable<byte[]> pcm, [EnumeratorCancellation] CancellationToken ct)
    {
        WorkflowRules.Require(status.Speech, "NotConfigured: Gemini Live Transcribe cần AI__ApiKey và Speech__Provider=Gemini.", 503);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var events = Channel.CreateBounded<LiveText>(new BoundedChannelOptions(16) { SingleReader = true, SingleWriter = false });
        var pump = Pump(pcm, events.Writer, lifetime.Token);
        try { await foreach (var item in events.Reader.ReadAllAsync(ct)) yield return item; }
        finally { lifetime.Cancel(); await pump; }
    }

    private async Task Pump(IAsyncEnumerable<byte[]> audio, ChannelWriter<LiveText> events, CancellationToken ct)
    {
        Exception? failure = null;
        try
        {
            await using var socket = sockets.Create();
            using var setup = CancellationTokenSource.CreateLinkedTokenSource(ct); setup.CancelAfter(TimeSpan.FromSeconds(10));
            await socket.Connect(config["AI:ApiKey"]!, setup.Token);
            await socket.Send(JsonSerializer.Serialize(new { setup = new { model = $"models/{status.LiveModel}",
                generationConfig = new { responseModalities = new[] { "TEXT" } }, inputAudioTranscription = new { languageCodes = new[] { "vi-VN" }, mode = "VERBATIM" } } }), setup.Token);
            using (var ready = Parse(await socket.Receive(setup.Token)))
            {
                WorkflowRules.Require(ready.RootElement.TryGetProperty("setupComplete", out _), "GeminiLiveSetupFailed: không nhận được setupComplete.", 502);
            }
            await events.WriteAsync(new("ready"), ct);
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var drainSeconds = Math.Clamp(config.GetValue("Speech:DrainTimeoutSeconds", 5), 1, 10);
            var sendingDone = false;
            var sender = SendAudio();
            try
            {
                while (true)
                {
                    string? json;
                    try { json = await socket.Receive(session.Token); }
                    catch (OperationCanceledException) when (Volatile.Read(ref sendingDone) && !ct.IsCancellationRequested) { break; }
                    if (json == null)
                    {
                        WorkflowRules.Require(Volatile.Read(ref sendingDone), "GeminiLiveDisconnected: kết nối ngắt trong khi đang thu; hãy thu tiếp phần còn thiếu.", 502);
                        break;
                    }
                    using var doc = Parse(json);
                    var root = doc.RootElement;
                    WorkflowRules.Require(!root.TryGetProperty("goAway", out _), "GeminiLiveGoAway: dịch vụ yêu cầu nối lại. Transcript đã xác nhận được giữ; thu tiếp phần còn thiếu.", 503);
                    if (!root.TryGetProperty("serverContent", out var content)) continue;
                    if (content.TryGetProperty("interimInputTranscription", out var partial))
                        await events.WriteAsync(new("interim", Text(partial)), ct);
                    if (content.TryGetProperty("inputTranscription", out var final))
                        await events.WriteAsync(new("final", Text(final)), ct);
                }
                await sender;
                await events.WriteAsync(new("complete"), ct);
            }
            finally { session.Cancel(); try { await sender; } catch (OperationCanceledException) { } }

            async Task SendAudio()
            {
                try
                {
                    var sentFrames = 0;
                    await foreach (var pcm in audio.WithCancellation(session.Token))
                    {
                        WorkflowRules.Require(pcm.Length is > 0 and <= 6400 && pcm.Length % 2 == 0, "LiveAudioInvalid: mỗi frame PCM16 tối đa 200ms.", 415);
                        await socket.Send(JsonSerializer.Serialize(new { realtimeInput = new { audio = new { data = Convert.ToBase64String(pcm), mimeType = "audio/pcm;rate=16000" } } }), session.Token);
                        if (++sentFrames % 10 == 0) await events.WriteAsync(new("audioAck", sentFrames.ToString(System.Globalization.CultureInfo.InvariantCulture)), session.Token);
                    }
                    await socket.Send("{\"realtimeInput\":{\"audioStreamEnd\":true}}", session.Token);
                    Volatile.Write(ref sendingDone, true);
                    session.CancelAfter(TimeSpan.FromSeconds(drainSeconds));
                }
                catch { session.Cancel(); throw; }
            }
        }
        catch (WorkflowException ex) { failure = ex; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { failure = new WorkflowException(504, "GeminiLiveTimeout: không kết nối được trong 10 giây."); }
        catch (OperationCanceledException) { failure = new OperationCanceledException(ct); }
        catch (WebSocketException) { failure = new WorkflowException(502, "GeminiLiveUnavailable: kiểm tra mạng, key, quyền model và quota. Không tự phát lại audio để tránh trùng transcript."); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { failure = new WorkflowException(502, "GeminiLiveInvalidResponse: Google trả sai định dạng."); }
        catch (Exception) { failure = new WorkflowException(502, "GeminiLiveFailed: phiên transcription bị ngắt; có thể nhập tay hoặc thu tiếp."); }
        finally { events.TryComplete(failure); }
    }

    private static JsonDocument Parse(string? json)
    {
        WorkflowRules.Require(json != null, "GeminiLiveClosed: đóng kết nối trước khi setup xong.", 502);
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var c) && c.TryGetInt32(out var number) ? number : 502;
            doc.Dispose(); throw new WorkflowException(code == 429 ? 429 : 502, $"GeminiLiveProviderError HTTP {code}; kiểm tra key/model/quota.");
        }
        return doc;
    }
    private static string Text(JsonElement value)
    {
        var text = value.GetProperty("text").GetString() ?? "";
        WorkflowRules.Require(text.Length <= 12000, "GeminiLiveInvalidResponse: transcript quá dài.", 502);
        return text;
    }
}
