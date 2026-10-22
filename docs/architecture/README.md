# Kiến trúc AIVES đang chạy

Mở `AIVES_ARCHITECTURE.drawio` bằng diagrams.net / draw.io: **File → Open From → Device**. Từng khối, chữ và mũi tên đều chỉnh sửa được; đây không phải ảnh dán trong file.

- `AIVES_ARCHITECTURE.svg`: bản vector để xem/in.
- `AIVES_ARCHITECTURE.png`: bản xem nhanh đã kiểm tra bố cục.
- Tạo lại XML và SVG: `node tools/create-architecture.mjs` (PNG là ảnh kiểm chứng riêng).

| Thành phần | Bằng chứng trong code |
|---|---|
| React SPA / điều hướng / gọi API | `src/App.jsx`, `src/api/client.js`, `src/auth/*` |
| Lớp giảng viên, ngân hàng câu hỏi và lịch | `Teaching.jsx`, `TeachingResources.jsx`, `Exams.jsx`, `LecturerDashboard.jsx` |
| Microphone / transcript / audio | `src/audio/liveMicrophone.js`, `src/pages/VivaRoom.jsx` |
| Một backend ASP.NET Core | `backend/AIVES.Api/Program.cs`, `Controllers/*`, `Hubs/ExamHub.cs` |
| Nghiệp vụ / thông báo / thời hạn | `Features/Lecturer/*`, `Features/Workflow/*` |
| MySQL qua EF Core / Pomelo | `Data/AivesDbContext.cs`, `Models/*`, đăng ký `UseMySql` trong Program |
| Gemini qua backend | `Integrations/VivaProviders.cs`, `GeminiLiveTranscriber.cs` và DI trong Program |
| File PDF/DOCX riêng tư trên server | `Features/Lecturer/DocumentStore.cs`; metadata ở bảng documents |

Chỉ hai nhãn mũi tên **Request / Response**. Mỗi ranh giới có một cặp mũi tên: React ↔ backend, backend ↔ MySQL, backend ↔ Gemini. Response ở SignalR bao gồm sự kiện server push; không hàm ý mỗi sự kiện đều do một HTTP request mới của sinh viên.

API key nằm ở backend. LLM sau khi Nộp đang tắt mặc định; hình thể hiện tích hợp có trong code, không khẳng định key/model/hạn mức đã được kiểm tra bằng request thật. Không vẽ Claude, Azure, Redis, queue hoặc lưu trữ cloud vì chúng không nằm trong luồng runtime hiện tại.

Ba project 3-layer cũ đã được xóa theo yêu cầu: `src/` chỉ dành cho React, `backend/AIVES.Api` là backend Monolithic duy nhất. Solution `AIVES.slnx` trỏ tới backend này. Sơ đồ không thay đổi vì trước đó đã mô tả đúng runtime hiện hành.
