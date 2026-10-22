# API keys và cấu hình AI thật

> **Cập nhật Gemini:** Speech active đã chuyển hoàn toàn sang Gemini Live Transcribe + Gemini TTS. Hướng dẫn cấu hình/kiểm thử đang áp dụng: [GEMINI_SETUP.md](GEMINI_SETUP.md). Các mục Azure bên dưới là ghi chép phiên bản trước, không còn là cấu hình provider đang chạy.

## Dịch vụ đã triển khai

Gemini REST làm LLM; Azure AI Speech REST cho STT + TTS tiếng Việt. Tất cả request vendor chạy từ backend. Thiếu cấu hình → `NotConfigured`, không có provider giả. `Configured` chỉ xác nhận có cấu hình, **không chứng minh key còn hạn, quota đủ hoặc model dùng được**.

## Lấy key

1. [Google AI Studio — tạo API key](https://aistudio.google.com/apikey): chọn/tạo Google Cloud project, tạo key cho Gemini API. Xem [API key và bảo mật](https://ai.google.dev/gemini-api/docs/api-key), [model hiện có](https://ai.google.dev/gemini-api/docs/models) và [billing](https://ai.google.dev/gemini-api/docs/billing). Chọn model có quyền `generateContent` của project, đặt đúng `AI:Model` không có tiền tố `models/`. Hạn mức free/paid tùy model, region và tài khoản. Không cố định model đã hết vòng đời trong code.
2. [Azure Portal](https://portal.azure.com/): tạo resource **Speech** trong subscription/resource group/region phù hợp; chọn pricing tier được tài khoản cho phép. Vào **Keys and Endpoint**, lấy Key và Location/Region như `southeastasia`. Hai giá trị phải cùng resource. Cần quyền tạo resource (ví dụ Contributor) hoặc quyền đọc keys được quản trị viên cấp. Nếu resource chỉ cho Entra/private network, adapter subscription-key/regional public REST này cần cấu hình tương thích; không tự thay đổi quyền/network của resource.
3. [Claude API keys](https://console.anthropic.com/settings/keys) là địa chỉ tham khảo trong đề bài. Phiên bản này **chỉ có adapter Gemini**; không đặt `AI:Provider=Claude` vì chưa triển khai Claude.

Không dán key/password vào chat, `src/`, `.env` Vite hay Git. Không đặt `VITE_AI_API_KEY`, `VITE_SPEECH_KEY`: mọi biến VITE có thể được bundle xuống trình duyệt.

## Dán key ở đâu khi chạy local

Chạy từ root repo, thay placeholder ngay trên máy. ASP.NET Core User Secrets được nạp trong Development, file secret nằm ngoài repo. `.csproj` đã có UserSecretsId, không cần init lại.

```powershell
dotnet user-secrets set "AI:Provider" "Gemini" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "AI:ApiKey" "<GEMINI_KEY_CUA_BAN>" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "AI:Model" "<MODEL_GENERATECONTENT_CON_HIEU_LUC>" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:Provider" "Azure" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:Key" "<AZURE_SPEECH_KEY_CUA_BAN>" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:Region" "<REGION_CUA_RESOURCE>" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=aives_db;User=<USER>;Password=<PASSWORD>;SslMode=Required" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Jwt:Key" "<CHUOI_NGAU_NHIEN_IT_NHAT_32_KY_TU>" --project backend/AIVES.Api/AIVES.Api.csproj
```

Local override JSON tùy chọn chỉ dùng cho kết nối DB/mức log đã chuẩn bị trên máy này. Thứ tự ưu tiên cuối cùng: appsettings → appsettings.Development → appsettings.Development.local.json → User Secrets (Development) → environment variables. User Secrets và environment được ưu tiên hơn JSON local. Các tool audit/smoke/probe dùng cùng thứ tự này. Keys AI/Speech không được thêm vào JSON local trong quá trình triển khai. Khởi động lại backend sau khi đổi keys/model.

Khi deploy, cấu hình bằng secret manager/environment backend:

```text
AI__Provider=Gemini
AI__ApiKey=<secret>
AI__Model=<model-id>
Speech__Provider=Azure
Speech__Key=<secret>
Speech__Region=<region>
ConnectionStrings__DefaultConnection=<mysql-connection>
Jwt__Key=<secret-at-least-32-characters>
Jwt__Issuer=AIVES
Jwt__Audience=AIVES
Cors__Origins__0=https://<frontend-domain>
```

Tên các biến trên khớp code. Không cần Speech endpoint tùy ý; adapter xây regional endpoint từ Region đã kiểm tra ký tự. Không đưa connection string vào React. Nếu JWT Issuer/Audience hiện có khác ví dụ, giữ hai đầu token/validation nhất quán.

## Kiểm tra gọi dịch vụ thật

Sau khi lưu key, khởi động lại backend. Đăng nhập rồi gọi GET `/api/workflow/providers` với Bearer token, UI phòng thi cũng hiện trạng thái này.

Probe độc lập, không ghi database, dùng cấu hình giống backend local:

```powershell
dotnet run --project tools/ProviderProbe/ProviderProbe.csproj
# Thêm file giọng nói tiếng Việt PCM WAV mono 16kHz 16bit, <=15 giây để test STT:
dotnet run --project tools/ProviderProbe/ProviderProbe.csproj -- --wav artifacts/sample-vi.wav
```

Probe gọi Gemini generateContent thật khi đủ key/model; Azure TTS thật khi đủ key/region, ghi `artifacts/provider-probe.mp3` để nghe lại. STT thật chỉ gọi khi có `--wav`. Không in key hay transcript. `SKIP` nghĩa là **chưa kiểm thử**, không phải PASS. Có thể phát sinh phí provider theo gói của bạn.

Test end-to-end: tạo và công bố lịch đúng giờ → login Student → bắt đầu → Nghe câu hỏi → cấp mic → nói tiếng Việt ít nhất một đoạn → đợi transcript → sửa nếu cần → nộp → xác nhận Gemini có thể hỏi thêm khi câu trả lời thiếu ý, không hỏi khi đủ ý → hoàn tất → refresh xem lịch sử. Thử thêm Brave và Chrome, microphone denied, mất mạng/reconnect và 429/quota.

STT REST không streaming: đoạn khoảng 8 giây + thời gian upload/provider; UI ghi rõ. Khi không có key/lỗi dịch vụ, mic/TTS có thể bị vô hiệu hoặc báo lỗi, nhập tay là fallback. Không lấy việc nhập tay thành bằng chứng STT/TTS hoạt động.

## API và tài liệu chính thức đã đối chiếu

- [Gemini generateContent](https://ai.google.dev/api/generate-content): POST `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, header `x-goog-api-key`, JSON output.
- [Azure short audio STT](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-speech-to-text-short): POST `https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=vi-VN&format=simple`, header `Ocp-Apim-Subscription-Key`, canonical WAV PCM 16kHz mono.
- [Azure TTS REST](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech): POST `https://{region}.tts.speech.microsoft.com/cognitiveservices/v1`, SSML, header output format MP3.
- [Ngôn ngữ và giọng đọc](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support): cấu hình STT `vi-VN`, TTS `vi-VN-HoaiMyNeural`.

Thông báo lỗi: 503 NotConfigured → bổ sung cấu hình; 502 HTTP 401/403 → key/quyền/region; 502 HTTP 429 → quota/rate limit, đã retry tối đa một lần; 504 → timeout 25 giây; 422 SpeechNoMatch → thử nói rõ hơn hoặc nhập tay. Không log response body vendor để tránh lộ thông tin.
