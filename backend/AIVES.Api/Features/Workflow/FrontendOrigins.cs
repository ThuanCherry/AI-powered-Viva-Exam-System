namespace AIVES.Api.Features.Workflow;

public static class FrontendOrigins
{
    public static string[] Resolve(IEnumerable<string> configured, bool development) =>
        (development ? configured.Concat(new[] {
            "http://localhost:5173", "http://127.0.0.1:5173",
            "http://localhost:5174", "http://127.0.0.1:5174"
        }) : configured).Distinct().ToArray();
}
