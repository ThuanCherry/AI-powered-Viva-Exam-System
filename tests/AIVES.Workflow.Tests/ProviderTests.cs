using System.Net;
using System.Text;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AIVES.Workflow.Tests;

public class ProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["AI:Provider"] = "Gemini", ["AI:ApiKey"] = "unit-test-key", ["AI:Model"] = "model-for-unit-test",
        ["Speech:Provider"] = "Gemini", ["Speech:DrainTimeoutSeconds"] = "1" }).Build();

    [Fact] public async Task GeminiUsesKeyHeaderAndValidatedStructuredOutput()
    {
        var config = Config();
        using var handler = new Handler(async request => {
            Assert.DoesNotContain("unit-test-key", request.RequestUri!.ToString());
            Assert.Equal("unit-test-key", request.Headers.GetValues("x-goog-api-key").Single());
            var input = await request.Content!.ReadAsStringAsync();
            Assert.Contains("systemInstruction", input); Assert.Contains("responseMimeType", input);
            var decision = System.Text.Json.JsonSerializer.Serialize(new { needsFollowUp = false, question = (string?)null, reason = (string?)null });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(new { candidates = new[] { new { content = new { parts = new[] { new { text = decision } } } } } }) };
        });
        var llm = new GeminiViva(config, new(config), new(new Factory(handler)));
        var result = await llm.Decide("Q", "secret", "Q", "A", "", default); Assert.False(result.NeedsFollowUp);
    }

    [Fact] public async Task GeminiTtsUsesInteractionsAndReturnsWavWithoutDoubleHeader()
    {
        var config = Config(); var calls = 0;
        var wav = PcmWave.Wrap(new byte[3200], 24000);
        using var handler = new Handler(async request => {
            calls++; Assert.Equal("unit-test-key", request.Headers.GetValues("x-goog-api-key").Single());
            Assert.Equal("/v1beta/interactions", request.RequestUri!.AbsolutePath);
            var text = await request.Content!.ReadAsStringAsync(); Assert.Contains("speech_metadata", text); Assert.Contains("gemini-3.8-flash-tts", text);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(new { steps = new[] { new { type = "model_output", content = new[] { new { type = "audio", mime_type = "audio/wav", data = Convert.ToBase64String(wav) } } } } }) };
        });
        var speech = new GeminiVivaSpeech(config, new(config), new(new Factory(handler)), new FakeLive());
        Assert.Equal(wav, await speech.Speak("Câu hỏi tiếng Việt", default));
        Assert.Equal("Xin chào", await speech.Transcribe(PcmWave.Wrap(new byte[3200], 16000), default)); Assert.Equal(1, calls);
    }

    private sealed class FakeLive : IGeminiLiveTranscriber
    {
        public async IAsyncEnumerable<LiveText> Stream(IAsyncEnumerable<byte[]> audio, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        { await foreach (var _ in audio.WithCancellation(ct)) { } yield return new("final", "Xin chào"); }
    }

    [Fact] public void PcmTtsIsWrappedAndInvalidAudioFailsExplicitly()
    {
        var pcm = new byte[4800]; var wav = PcmWave.Output(pcm, "audio/L16;codec=pcm;rate=24000");
        Assert.Equal(4844, wav.Length); Assert.Equal(24000, BitConverter.ToInt32(wav, 24));
        var input = PcmWave.ToInput(wav); PcmWave.ValidateInput(input);
        Assert.Equal(16000, BitConverter.ToInt32(input, 24));
        Assert.Throws<WorkflowException>(() => PcmWave.Output([], "audio/wav"));
        Assert.Throws<WorkflowException>(() => PcmWave.Output(new byte[4], "audio/wav"));
    }

    [Fact] public async Task Provider429RetriesOnceAndNeverLeaksBody()
    {
        var calls = 0;
        using var handler = new Handler(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("private-provider-body") }); });
        var http = new ProviderHttp(new Factory(handler));
        var error = await Assert.ThrowsAsync<WorkflowException>(() => http.Send(() => new(HttpMethod.Get, "https://example.invalid"), default));
        Assert.Equal(2, calls); Assert.Equal(502, error.Status); Assert.DoesNotContain("private-provider-body", error.Message);
    }

    [Fact] public async Task ProviderTimeoutAndMalformedGeminiAreExplicitFailures()
    {
        using var timeoutHandler = new Handler(_ => throw new TaskCanceledException());
        Assert.Equal(504, (await Assert.ThrowsAsync<WorkflowException>(() => new ProviderHttp(new Factory(timeoutHandler)).Send(() => new(HttpMethod.Get, "https://example.invalid"), default))).Status);
        var config = Config();
        using var invalid = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad-json") }));
        var llm = new GeminiViva(config, new(config), new(new Factory(invalid)));
        Assert.Equal(502, (await Assert.ThrowsAsync<WorkflowException>(() => llm.Decide("Q", "E", "Q", "A", "", default))).Status);
    }

    private static StringContent Json(object value) => new(System.Text.Json.JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
