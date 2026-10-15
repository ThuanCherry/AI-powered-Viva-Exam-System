namespace AIVES.Api.Integrations.STT;

/// <summary>
/// Abstraction for Speech-to-Text services.
/// </summary>
public interface ISttService
{
    Task<string> TranscribeAsync(Stream audioStream, string contentType, CancellationToken cancellationToken = default);
}

public class PlaceholderSttService : ISttService
{
    public Task<string> TranscribeAsync(Stream audioStream, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult("[STT not configured] Transcription not available.");
}
