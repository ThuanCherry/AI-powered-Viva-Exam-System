namespace AIVES.Api.Integrations.TTS;

/// <summary>
/// Abstraction for Text-to-Speech services.
/// </summary>
public interface ITtsService
{
    Task<Stream> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
}

public class PlaceholderTtsService : ITtsService
{
    public Task<Stream> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(new MemoryStream());
}
