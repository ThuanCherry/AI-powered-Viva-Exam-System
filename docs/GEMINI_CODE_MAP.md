# Gemini: cấu hình và lõi AI

- Key/model: `backend/AIVES.Api/appsettings.json`, phần `AI` (`ApiKey`, `Model`, `Provider`) và `Speech` (`LiveModel`, `TtsModel`, `Voice`). Đổi key tại `AI:ApiKey`, lưu và khởi động lại backend. Đây là key thật dùng local; không commit/push file chứa key.
- Khi `AI:ApiKey` trong appsettings có giá trị, Development ưu tiên các cấu hình AI/Speech trong file này hơn User Secrets. Biến môi trường backend vẫn ưu tiên cuối cùng. Cấu hình MySQL/JWT giữ nguyên.
- Nạp cấu hình: `backend/AIVES.Api/Features/Workflow/LocalConfiguration.cs` và `Program.cs`.
- LLM sinh câu đào sâu: lớp `GeminiViva` trong `backend/AIVES.Api/Integrations/VivaProviders.cs`.
- TTS đọc câu hỏi: lớp `GeminiVivaSpeech` trong cùng file.
- STT tiếng Việt: `backend/AIVES.Api/Integrations/GeminiLiveTranscriber.cs`.
- Đăng ký DI cho các provider: `backend/AIVES.Api/Program.cs`.
- Quy tắc thi/giới hạn hỏi đào sâu: `backend/AIVES.Api/Features/Workflow/VivaEngine.cs`. React gọi backend, không giữ API key.

Trả lời khi thầy hỏi: “Em cấu hình key và model Gemini trong appsettings.json của backend C#. Các lớp trong Integrations gọi Gemini bằng HttpClient/WebSocket; Program.cs đăng ký chúng qua DI. VivaEngine quản lý luồng thi và giới hạn câu hỏi.”
