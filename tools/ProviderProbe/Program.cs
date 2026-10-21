using AIVES.Api.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Sends only the built-in test question/transcript (and optional supplied WAV) to configured providers.
var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("backend/AIVES.Api"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.local.json", true)
    .AddUserSecrets("AIVES-Workflow-Local", true).AddEnvironmentVariables().Build();
var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config);
services.AddHttpClient("providers"); services.AddSingleton<ProviderHttp>(); services.AddSingleton<ProviderStatus>();
services.AddSingleton<IGeminiSocketFactory, GeminiSocketFactory>(); services.AddSingleton<IGeminiLiveTranscriber, GeminiLiveTranscriber>();
services.AddSingleton<GeminiModelCatalog>();
services.AddTransient<IVivaLlm, GeminiViva>(); services.AddTransient<IVivaSpeech, GeminiVivaSpeech>();
await using var provider = services.BuildServiceProvider();
var status = provider.GetRequiredService<ProviderStatus>();
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(status.View));
if (args.Contains("--models"))
{
    try
    {
        var models = await provider.GetRequiredService<GeminiModelCatalog>().Read(default);
        foreach (var selected in new[] { config["AI:Model"], status.LiveModel, status.TtsModel }.Where(m => m != null))
        {
            var model = models.FirstOrDefault(m => m.Name == "models/" + selected);
            Console.WriteLine($"Model {selected}: {(model == null ? "NOT LISTED" : string.Join(",", model.Methods))}");
        }
        Console.WriteLine("Quota: verify this project's active limits in AI Studio. Model inventory does not establish remaining quota.");
    }
    catch (Exception ex) { Console.WriteLine($"FAIL model inventory: {ex.Message}"); Environment.ExitCode = 1; }
    return;
}
if (status.Llm)
{
    try
    {
        var decision = await provider.GetRequiredService<IVivaLlm>().Decide("Transaction là gì?", "Một tập thao tác có tính nguyên tử, nhất quán, cô lập và bền vững.", "Transaction là gì?", "Là một số câu lệnh chạy cùng nhau.", "", default);
        Console.WriteLine($"PASS live Gemini: valid structured decision; needsFollowUp={decision.NeedsFollowUp}");
    }
    catch (Exception ex) { Console.WriteLine($"FAIL live Gemini: {ex.Message}"); Environment.ExitCode = 1; }
}
else Console.WriteLine("SKIP live Gemini: NotConfigured (key + model required)");
if (status.Speech)
{
    try
    {
        var speech = provider.GetRequiredService<IVivaSpeech>();
        var audio = await speech.Speak("Tôi đang kiểm tra nhận dạng tiếng Việt trong hệ thống vấn đáp.", default);
        if (audio.Length == 0) throw new Exception("Empty TTS audio");
        Directory.CreateDirectory("artifacts"); await File.WriteAllBytesAsync("artifacts/provider-probe.wav", audio);
        Console.WriteLine($"PASS live Gemini TTS: {audio.Length} WAV bytes, artifacts/provider-probe.wav; listen manually to verify voice quality.");
        if (args.Contains("--roundtrip") || args.Length == 2 && args[0] == "--wav")
        {
            var wav = args.Contains("--roundtrip") ? PcmWave.ToInput(audio) : await File.ReadAllBytesAsync(args[1]);
            await File.WriteAllBytesAsync("artifacts/provider-probe-input.wav", wav);
            var text = await speech.Transcribe(wav, default);
            Console.WriteLine($"PASS live Gemini Live Transcribe: received {text.Length} final transcript characters; transcript is not logged.");
        }
        else Console.WriteLine("SKIP live STT: supply --roundtrip (synthesized fixture) or --wav <16kHz-mono-16bit-PCM.wav>.");
    }
    catch (Exception ex) { Console.WriteLine($"FAIL live Gemini speech: {ex.Message}"); Environment.ExitCode = 1; }
}
else Console.WriteLine("SKIP live Gemini STT/TTS: NotConfigured (AI key + Gemini provider required)");
