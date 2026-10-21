using System.Text.Json;
using System.Threading.Channels;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AIVES.Workflow.Tests;

public class LiveTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["AI:Provider"] = "Gemini", ["AI:ApiKey"] = "unit-test-only", ["Speech:Provider"] = "Gemini", ["Speech:DrainTimeoutSeconds"] = "1" }).Build();
    private sealed class Socket : IGeminiSocket
    {
        private readonly Channel<string> responses = Channel.CreateUnbounded<string>();
        public List<string> Sent { get; } = [];
        public bool Fail;
        public bool Disposed;
        public Task Connect(string key, CancellationToken ct) { Assert.Equal("unit-test-only", key); return Task.CompletedTask; }
        public Task Send(string json, CancellationToken ct)
        {
            Sent.Add(json);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("setup", out _))
                responses.Writer.TryWrite(Fail ? "{\"error\":{\"code\":429,\"message\":\"private body\"}}" : "{\"setupComplete\":{}}");
            else if (json.Contains("audioStreamEnd"))
            { responses.Writer.TryWrite("{\"serverContent\":{\"inputTranscription\":{\"text\":\"Xin chào Việt Nam\"}}}"); responses.Writer.TryComplete(); }
            else responses.Writer.TryWrite("{\"serverContent\":{\"interimInputTranscription\":{\"text\":\"Xin ch\"}}}");
            return Task.CompletedTask;
        }
        public async Task<string?> Receive(CancellationToken ct) => await responses.Reader.WaitToReadAsync(ct) ? await responses.Reader.ReadAsync(ct) : null;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    private sealed class Factory(Socket socket) : IGeminiSocketFactory { public IGeminiSocket Create() => socket; }
    private static async IAsyncEnumerable<byte[]> Audio(int size = 3200) { yield return new byte[size]; await Task.Yield(); }

    [Fact] public async Task LiveProtocolUsesVietnameseTextModalityAndCommitsOnlyFinal()
    {
        var config = Config(); var socket = new Socket(); var live = new GeminiLiveTranscriber(config, new(config), new Factory(socket));
        var events = new List<LiveText>(); await foreach (var item in live.Stream(Audio(), default)) events.Add(item);
        Assert.Equal("ready", events[0].Type); Assert.Contains(events, e => e.Type == "interim" && e.Text == "Xin ch");
        Assert.Single(events, e => e.Type == "final"); Assert.Equal("complete", events[^1].Type);
        using var setup = JsonDocument.Parse(socket.Sent[0]); var root = setup.RootElement.GetProperty("setup");
        Assert.Equal("models/gemini-3.5-transcribe-live", root.GetProperty("model").GetString());
        Assert.Equal("TEXT", root.GetProperty("generationConfig").GetProperty("responseModalities")[0].GetString());
        Assert.Equal("vi-VN", root.GetProperty("inputAudioTranscription").GetProperty("languageCodes")[0].GetString());
        Assert.DoesNotContain("unit-test-only", socket.Sent[0]); Assert.Contains(socket.Sent, m => m.Contains("audio/pcm;rate=16000")); Assert.True(socket.Disposed);
    }
    [Fact] public async Task Live429IsSanitizedAndReleasesSocket()
    {
        var config = Config(); var socket = new Socket { Fail = true }; var live = new GeminiLiveTranscriber(config, new(config), new Factory(socket));
        var ex = await Assert.ThrowsAsync<WorkflowException>(async () => { await foreach (var _ in live.Stream(Audio(), default)) { } });
        Assert.Equal(429, ex.Status); Assert.DoesNotContain("private body", ex.Message); Assert.True(socket.Disposed);
    }
    [Fact] public void LiveCapacityAndOneStreamPerCandidateReleaseCorrectly()
    {
        var limits = new LiveStreamLimits(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Speech:MaxConcurrentStreams"] = "2" }).Build());
        using (limits.Enter(1, 2))
        {
            Assert.Equal(409, Assert.Throws<WorkflowException>(() => limits.Enter(1, 2)).Status);
            using var second = limits.Enter(1, 3);
            Assert.Equal(429, Assert.Throws<WorkflowException>(() => limits.Enter(2, 4)).Status);
        }
        using var next = limits.Enter(1, 2);
    }
    [Fact] public async Task LiveTranscriptPersistsDeduplicatesAndRejectsClosedTurn()
    {
        await using var f = await Fixture.Create(); var id = await f.Published();
        var state = Fixture.Json(await f.Engine.Start(id, 2, default)); var turn = state.GetProperty("turn").GetProperty("turnId").GetString()!;
        var grant = await f.Engine.AudioGrant(id, 2, turn, default); Assert.True(grant.Deadline > f.Clock.Value.UtcDateTime);
        await f.Engine.PersistLiveTranscript(id, 2, turn, "server-session:1", "Đoạn đã xác nhận.", default);
        await f.Engine.PersistLiveTranscript(id, 2, turn, "server-session:1", "Không được ghi đè.", default);
        f.Db.ChangeTracker.Clear();
        var resumed = Fixture.Json(await f.Engine.Resume(id, 2, default)); Assert.Equal("Đoạn đã xác nhận.", resumed.GetProperty("turn").GetProperty("transcript").GetString());
        f.Clock.Value = f.Clock.Value.AddSeconds(31);
        await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.PersistLiveTranscript(id, 2, turn, "server-session:2", "muộn", default));
        await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.AudioGrant(id, 3, turn, default));
    }
}
