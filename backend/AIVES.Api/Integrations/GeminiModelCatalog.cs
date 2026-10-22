using System.Text.Json;
using AIVES.Api.Features.Workflow;

namespace AIVES.Api.Integrations;

public record GeminiModelInfo(string Name, string[] Methods);
public class GeminiModelCatalog(IConfiguration config, ProviderStatus status, ProviderHttp http)
{
    public async Task<IReadOnlyList<GeminiModelInfo>> Read(CancellationToken ct)
    {
        WorkflowRules.Require(status.KeyConfigured, "NotConfigured: cần AI__ApiKey để kiểm tra quyền model.", 503);
        var models = new List<GeminiModelInfo>(); string? token = null;
        do
        {
            var url = "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000" + (token == null ? "" : "&pageToken=" + Uri.EscapeDataString(token));
            var bytes = await http.Send(() => { var r = new HttpRequestMessage(HttpMethod.Get, url); r.Headers.Add("x-goog-api-key", config["AI:ApiKey"]); return r; }, ct);
            try
            {
                using var doc = JsonDocument.Parse(bytes);
                foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
                    models.Add(new(model.GetProperty("name").GetString()!, model.TryGetProperty("supportedGenerationMethods", out var methods) ? methods.EnumerateArray().Select(m => m.GetString()!).ToArray() : []));
                token = doc.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
                WorkflowRules.Require(models.Count <= 10000, "Model catalog quá lớn.", 502);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            { throw new WorkflowException(502, "GeminiModelsInvalidResponse."); }
        } while (!string.IsNullOrEmpty(token));
        return models;
    }
}
