using AIVES.Api.Features.Common;
using AIVES.Api.Integrations;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Admin;

public record UpdateSpeechRequest(string Language, string? Voice = null);

// Reports effective configuration and allows runtime language/voice switching. Never probes providers or returns API keys.
public class AdminSettingsService(ProviderStatus status, IConfiguration config)
{
    public object Read(Caller caller)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được xem cấu hình hệ thống.", 403);
        return new
        {
            providers = status.View,
            llmModel = config["AI:Model"],
            voice = status.Voice,
            language = status.Language,
            supportedLanguages = new[]
            {
                new { code = "vi-VN", label = "Tiếng Việt (vi-VN)" },
                new { code = "en-US", label = "Tiếng Anh (en-US)" }
            },
            supportedVoices = new[] { "Kore", "Aoede", "Puck", "Charon", "Fenrir" },
            readOnly = true,
            configurationFile = "backend/AIVES.Api/appsettings.json"
        };
    }

    public object UpdateSpeech(UpdateSpeechRequest request, Caller caller)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được cập nhật cấu hình ngôn ngữ.", 403);
        var lang = request.Language?.Trim();
        Require(lang is "vi-VN" or "en-US", "Ngôn ngữ chỉ hỗ trợ Tiếng Việt (vi-VN) hoặc Tiếng Anh (en-US).");
        var voice = string.IsNullOrWhiteSpace(request.Voice) ? status.Voice : request.Voice.Trim();
        Require(voice is "Kore" or "Aoede" or "Puck" or "Charon" or "Fenrir", "Giọng đọc không hợp lệ.");
        status.UpdateSpeech(lang!, voice);
        return Read(caller);
    }
}
