# Chạy thật và kiểm thử

> Speech hiện tại: **Gemini Live Transcribe + Gemini TTS + Gemini LLM**, đã test bằng key thực. Hướng dẫn và báo cáo mới nhất: [GEMINI_SETUP.md](GEMINI_SETUP.md). Báo cáo NotConfigured/Azure ở phần bàn giao cũ bên dưới chỉ mô tả trạng thái trước lần tích hợp Gemini này.

## Điều kiện và MySQL

Cài .NET SDK 10, Node/npm tương thích Vite 8, MySQL 8. Chạy lệnh trong root repo. Ứng dụng tái sử dụng schema `aives_db` trên máy hiện tại; server MySQL đã được kết nối thực. Không cần import dump hay chạy migration.

Nếu MySQL Windows service đang dừng, tìm đúng service rồi khởi động (cần quyền hệ thống):

```powershell
Get-Service *mysql*
Start-Service -Name <TEN_SERVICE_THUC_TE>
```

Nếu bạn dùng MySQL ở máy khác/container, khởi động instance có dữ liệu hiện có bằng cách vận hành của bạn. Không tạo container DB rỗng rồi tưởng đã nối schema hiện tại. Cấu hình connection bằng User Secrets/env theo [AI_KEYS_AND_SETUP.md](AI_KEYS_AND_SETUP.md). Không dùng root cho deploy; cấp user ứng dụng quyền đọc/ghi dữ liệu các bảng hiện có, không cần DROP/CREATE/ALTER.

Kiểm tra chỉ đọc:

```powershell
dotnet run --project tools/DbAudit/DbAudit.csproj
dotnet run --project tools/DbAudit/DbAudit.csproj -- --list-databases
# Chọn schema để audit, không thay cấu hình/không import:
dotnet run --project tools/DbAudit/DbAudit.csproj -- --database aives_db
```

1049 = tên database không tồn tại; 1042/connection refused = kiểm tra host/port/service/quyền mạng; 1045 = credentials/quyền. Tool không in connection string/password. `/health` thực sự thử kết nối DB; server có thể lên kể cả khi DB chưa kết nối, health trả 503 và các thao tác dữ liệu sẽ lỗi cho tới khi cấu hình đúng.

## Ba lệnh khởi động

Sau khi MySQL đang chạy và cấu hình backend xong:

```powershell
npm ci
dotnet run --project backend/AIVES.Api
npm run dev
```

Chạy hai lệnh cuối trong **hai terminal riêng**, đều tại root repo. `dotnet run` tự restore/build backend; npm ci cài đúng lockfile. Mở [React local](http://localhost:5173), backend [health](http://localhost:5234/health), [Swagger Development](http://localhost:5234/swagger).

Vite proxy `/api`, `/hubs` (WebSocket) và `/health` sang `localhost:5234`. Copy `.env.example` thành `.env.local` nếu cần; để `VITE_API_BASE_URL=` trống thì dùng proxy cùng origin. `.env` có sẵn trên máy đang trỏ `http://localhost:5234`, cũng hoạt động qua CORS localhost:5173. Development cho phép thêm 127.0.0.1:5173; deploy cấu hình Cors:Origins đúng hostname, HTTPS và WebSocket proxy. Không dùng Vite dev server làm production server.

Backend local override chọn `aives_db` được gitignore; `appsettings.json` có thay đổi trước task được giữ nguyên, không stage/commit secrets từ file này. `.env` đã được Git theo dõi từ trước và hiện chỉ chứa URL frontend; thêm `.gitignore` không bỏ tracking của file có sẵn. File này được giữ nguyên, không thêm secret vào đó. Dùng `.env.local` gitignored cho tùy chỉnh React mới và User Secrets/env cho DB/JWT/AI. Không có tự tạo schema hoặc tự seed lúc boot.

## Tài khoản và dữ liệu demo

Không hardcode mật khẩu/tài khoản seed. Audit ban đầu DB schema có sẵn nhưng chưa có users/questions để demo. Công cụ sau **thêm mới** Lecturer, Lecturer đối chứng, hai Student, môn/lớp/enrollment và chín câu Approved có tên DEMO; không ghi đè tài khoản/dữ liệu cũ, không cleanup bằng DELETE:

```powershell
dotnet run --project tools/WorkflowSmoke/WorkflowSmoke.csproj -- --seed-only
```

Mỗi lần chạy tạo fixture mới có timestamp/ngẫu nhiên. Thông tin đăng nhập nằm trong **`artifacts/demo-credentials.local.json`**, gitignored. Mật khẩu ngẫu nhiên, không in console; có thể đặt `AIVES_DEMO_PASSWORD` ở environment local trước khi seed nếu muốn tự chọn. Không đưa file credentials vào Git/chat/tài liệu công khai. Backend không có endpoint nâng quyền: tool chỉ là CLI quản trị local cho demo.

Nếu đã chạy smoke trong bàn giao, file này đã có sẵn. Đăng nhập bằng trường `lecturer` hoặc `students[0]` với `password` trong file. Khóa provider/JWT không được ghi vào file credentials. Dữ liệu demo được giữ lại; chỉ xóa khi có yêu cầu/đồng ý riêng của chủ DB.

## Test tự động

```powershell
dotnet restore backend/AIVES.Api/AIVES.Api.csproj
dotnet build backend/AIVES.Api/AIVES.Api.csproj --no-restore
dotnet test tests/AIVES.Workflow.Tests/AIVES.Workflow.Tests.csproj --verbosity quiet
npm run build
npm run lint
git diff --check
```

Tests dùng SQLite `:memory:` riêng, mock LLM/Speech, clock kiểm soát được. `EnsureCreated` chỉ xuất hiện trong fixture test SQLite, không chạy trên MySQL. Tests kiểm tra lịch, quyền chủ môn/enrollment, pool thiếu, chống trùng phân câu, start/retry/resume, follow-up tổng/theo câu, đủ ý không follow-up, timeout server, persistence và provider failure. Không dùng mock để chứng minh gọi vendor thật.

Smoke HTTP (backend đang chạy):

```powershell
dotnet run --project tools/WorkflowSmoke/WorkflowSmoke.csproj
```

Công cụ thêm fixture mới rồi gọi API thật bằng JWT: role guards, Draft/Scheduled, GET chi tiết, sửa/validate conflict, công bố idempotent, student schedule, start sớm/bị cấm/start đồng thời, retry đáp án đồng thời, hoàn tất và đọc lại persistence MySQL. Để đổi backend dùng `AIVES_BASE_URL`; connection mà tool seed dùng phải cùng DB backend. Nó không mock Gemini/Azure. Thiếu key sẽ báo SKIP live, backend lưu NotConfigured và tiếp tục fallback văn bản. Chạy probe vendor ở [AI_KEYS_AND_SETUP.md](AI_KEYS_AND_SETUP.md) khi có key. Probe có thể tính phí trên tài khoản provider.

## Demo thủ công đầy đủ

1. Lecturer → Tạo kỳ thi → chọn môn DEMO, đặt start cách hiện tại 1 phút hoặc trong slot hiện tại, end đủ hai slot; chọn một/hai Student.
2. Cấu hình câu chính=2, tổng follow-up=2, mỗi câu=1, giây/lượt=120, Random. Preview phải hiện thứ tự/tổng phút. Lưu nháp; kiểm tra danh sách thật. Nếu báo conflict, chọn khung không trùng các lịch chưa hoàn thành.
3. Công bố. Reload trang giảng viên: Scheduled, không sửa nữa. Đăng nhập Lecturer đối chứng: không truy cập được kỳ thi này.
4. Student chỉ thấy lịch của mình. Trước giờ start báo 409 rõ ràng. Đúng giờ vào phòng; refresh/start lại giữ cùng lượt.
5. Có Azure: nghe TTS, thu âm >8 giây, đợi STT, xem/sửa transcript. Không có Azure: UI NotConfigured, nhập tay dự phòng. Không tuyên bố mic đã tích hợp thành công nếu chưa chạy bước này với key.
6. Có Gemini: trả lời thiếu ý để kiểm tra follow-up, đủ ý để kiểm tra chuyển câu; backend không vượt hai mức ngân sách. Thiếu Gemini: lỗi NotConfigured được lưu và chuyển câu chính tiếp theo, không tạo follow-up giả.
7. Nộp hết câu: Completed, refresh còn lịch sử. Mất mạng/đóng tab/mở lại: tiếp tục từ server; đồng hồ không reset. Transcript gõ tay chưa nộp chưa được lưu.
8. Thử microphone denied, MIME sai, audio quá lớn, provider 401/429/timeout, slot hết giờ; không lộ đáp án/rubric/token/key trong JSON cho Student. Khi mất hub phải thấy trạng thái REST dự phòng.

## Kết quả và giới hạn bàn giao

Kết quả bàn giao ngày **19/10/2026 (Asia/Ho_Chi_Minh)**:

- Backend restore/build thành công, 0 warnings/errors; EF Core 9.0.20 + Pomelo 9.0.0 trên .NET 10.
- **20/20 tests pass**, gồm SQLite workflow/persistence + adapter HTTP mocked; không gọi chúng là test vendor live.
- **37 kiểm tra HTTP pass** trên backend thật + MySQL **8.0.46 / aives_db**, gồm start/retry đồng thời và STT/TTS NotConfigured trả 503.
- React `npm run build`, `npm run lint`, `git diff --check` đều thành công.
- Kiểm tra trình duyệt thực: Lecturer login → sửa nháp/chọn hai sinh viên/preview → công bố; Student login → phòng chờ → kết nối SignalR → bắt đầu → nộp hai câu bằng fallback văn bản → Completed → refresh giữ nguyên lịch sử. Ảnh xác nhận local `artifacts/workflow-student-completed.png`.
- Probe Gemini/Azure chạy và báo **SKIP / NotConfigured**, vì không có key/model/region. Chưa kiểm chứng provider live, thu âm/transcription, voice quality/latency hoặc microphone trên Brave/Chrome. Không có tuyên bố voice end-to-end đã pass.

Hai nhánh giọng nói/vendor cần keys của chủ dự án để xác nhận chất lượng/latency thực, Brave/Chrome microphone và quota. ProviderProbe phân biệt PASS/FAIL/SKIP; tuyệt đối không tính SKIP thành PASS. Configured cũng không có nghĩa key hợp lệ.

Adaptive là cân bằng độ khó, không dùng mô hình năng lực. Follow-up AI và transcript được lưu nhưng không sinh điểm/final_results tự động. Raw audio không được lưu; không có tính năng xem recording giả. Lock theo exam giới hạn concurrency, phù hợp lịch tuần tự; tránh mở nhiều lượt đồng thời cùng exam nếu cần throughput lớn mà chưa đo tải. File legacy được giữ, không xóa/migrate tự động.

## Nhóm commit đề xuất (chưa commit/push/merge)

1. `feat(schedule): MySQL-backed draft, scheduling and persistent question allocation`
2. `feat(viva): server-timed attempts, Gemini/Azure adapters and authorized events`
3. `feat(ui): lecturer editor and student voice/text interview with JWT`
4. `test(docs): workflow tests, real MySQL smoke and setup guides`

Review diff và secrets trước khi stage. Giữ thay đổi cấu hình cá nhân có trước ngoài các commit này; không đưa `.env`, `.vs`, artifacts hoặc JSON local vào Git.
