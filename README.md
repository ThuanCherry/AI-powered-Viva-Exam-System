# AI-powered-Viva-Exam-System

React JSX + ASP.NET Core monolith + MySQL hiện có.

- [Workflow giảng viên, API và demo cô lập không gọi AI](docs/LECTURER_WORKFLOW.md)
- [Chạy thật, tài khoản demo và kiểm thử](docs/RUN_AND_TEST.md)
- [Workflow lịch thi và AI vấn đáp, API, states](docs/AIVES_WORKFLOW_2_3.md)
- [Cấu hình keys Gemini/Azure ở backend](docs/AI_KEYS_AND_SETUP.md)
- [Gemini Live Transcribe + TTS + LLM: cấu hình và demo hiện tại](docs/GEMINI_SETUP.md)

Tại root repo, sau khi MySQL đang chạy và cấu hình connection/JWT:

```powershell
npm ci
dotnet run --project backend/AIVES.Api
npm run dev
```

Hai lệnh cuối chạy trong hai terminal riêng. Mở http://localhost:5173.
Không tự import SQL dump: dump chứa DROP TABLE. Xem hướng dẫn chạy trước khi thao tác DB.
