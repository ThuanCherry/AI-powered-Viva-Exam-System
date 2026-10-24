using AIVES.Api.Features.Common;
using AIVES.Api.Integrations;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Admin;

// Reports effective configuration only. Never probes providers or returns API keys.
public class AdminSettingsService(ProviderStatus status, IConfiguration config)
{
    public object Read(Caller caller)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được xem cấu hình hệ thống.", 403);
        return new { providers = status.View, llmModel = config["AI:Model"], voice = status.Voice,
            readOnly = true, configurationFile = "backend/AIVES.Api/appsettings.json" };
    }
}
