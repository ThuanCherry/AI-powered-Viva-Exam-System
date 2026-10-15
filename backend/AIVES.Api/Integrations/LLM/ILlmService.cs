namespace AIVES.Api.Integrations.LLM;

/// <summary>
/// Abstraction for LLM providers (Claude, Gemini, etc.).
/// Implementations will be registered when API keys are available.
/// </summary>
public interface ILlmService
{
    Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> EvaluateAnswerAsync(string question, string expectedAnswer, string studentAnswer, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(string question, string studentAnswer, int count = 1, CancellationToken cancellationToken = default);
}

/// <summary>
/// Placeholder implementation that returns not-configured messages.
/// Replace with actual Claude/Gemini SDK calls when credentials are available.
/// </summary>
public class PlaceholderLlmService : ILlmService
{
    public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("[LLM not configured] No response generated.");

    public Task<string> EvaluateAnswerAsync(string question, string expectedAnswer, string studentAnswer, CancellationToken cancellationToken = default)
        => Task.FromResult("[LLM not configured] Evaluation not available.");

    public Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(string question, string studentAnswer, int count = 1, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
