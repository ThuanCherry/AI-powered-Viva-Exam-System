# AI-powered-Viva-Exam-System

React JSX + ASP.NET Core monolith + MySQL hiện có.

- [Kiến trúc hiện tại — draw.io chỉnh sửa được](docs/architecture/AIVES_ARCHITECTURE.drawio) · [Xem SVG](docs/architecture/AIVES_ARCHITECTURE.svg)
- [Kết quả tích hợp dev và kiểm thử workflow 2](docs/MERGE_DEV_WORKFLOW2.md)

- [Workflow giảng viên, API và demo cô lập không gọi AI](docs/LECTURER_WORKFLOW.md)
- [Chạy thật, tài khoản demo và kiểm thử](docs/RUN_AND_TEST.md)
- [Workflow lịch thi và AI vấn đáp, API, states](docs/AIVES_WORKFLOW_2_3.md)
- [Cấu hình keys Gemini/Azure ở backend](docs/AI_KEYS_AND_SETUP.md)
- [Gemini Live Transcribe + TTS + LLM: cấu hình và demo hiện tại](docs/GEMINI_SETUP.md)

Tại root repo, sau khi MySQL đang chạy và cấu hình connection/JWT:

Máy mới: nếu chưa có `backend/AIVES.Api/appsettings.json`, sao chép từ `appsettings.example.json` cùng thư mục và điền MySQL/JWT. Không ghi đè cấu hình local có sẵn. API key thật chỉ nằm trong file local bị Git bỏ qua hoặc User Secrets.

```powershell
npm ci
dotnet run --project backend/AIVES.Api
npm run dev
```

Hai lệnh cuối chạy trong hai terminal riêng. Mở http://localhost:5173.
Không tự import SQL dump: dump chứa DROP TABLE. Xem hướng dẫn chạy trước khi thao tác DB.

Các project `src/AIVES.PresentationLayer`, `src/AIVES.BusinessLogicLayer`, `src/AIVES.DataAccessLayer` được giữ từ lịch sử dev. Ứng dụng React hiện tại dùng backend `backend/AIVES.Api`; không chạy đồng thời backend 3-layer thay thế khi test luồng này. Xem [kiến trúc runtime hiện tại](docs/architecture/README.md).
