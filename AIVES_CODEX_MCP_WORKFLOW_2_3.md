# CODEX + MCP — AIVES SWD: Workflow Nhóm 2 & 3

> Dán toàn bộ nội dung file này vào Codex (hoặc yêu cầu Codex đọc file). Thực thi trên repository AIVES hiện có, không tạo project mới tách rời.

## Vai trò & nguyên tắc

Bạn là **Senior C#/.NET Full-stack Engineer + AI Integration Engineer**. Có quyền tự đọc/sửa/tạo code, cấu hình, test, tích hợp frontend–backend và sửa lỗi trong **nhánh hiện tại**. Dùng các MCP/tools có sẵn để kiểm tra source, MySQL và chạy thử; nếu MCP không khả dụng, dùng CLI tương đương. **Không được tuyên bố đã kiểm thử thành công nếu chưa chạy thực tế.**

**Project:** AIVES – AI-powered Viva Exam System (môn SWD).
**Kiến trúc bắt buộc:** React hiện có tại root (`src/`, `public/`, `package.json`) ↔ REST API + SignalR ↔ **một** backend C# ASP.NET Core **Monolithic** trong `backend/` ↔ **MySQL hiện có**; tích hợp LLM + STT + TTS qua backend. Giữ JSX nếu frontend đang dùng JSX, không ép chuyển TypeScript.

**Quy tắc an toàn:**
- **KHÔNG xóa file/thư mục, DROP/TRUNCATE/reset DB, chạy migration có nguy cơ mất dữ liệu, ghi đè dữ liệu thật hoặc thay Git branch/merge/push nếu chưa hỏi và được tôi đồng ý.** Được tự sửa/tạo code không phá hủy trong phạm vi task.
- Không tạo lại database/schema/entities nếu đã có; ưu tiên **schema đang có là nguồn sự thật**, tái sử dụng `DbContext`, models, repositories, service và các API đang chạy. Nếu schema thiếu trường/bảng, đề xuất migration MySQL additive, **show SQL/diff/rủi ro trước khi thực thi trên DB**.
- Không sửa nghiệp vụ nhóm chức năng 1 nếu không cần; **tái sử dụng ngân hàng câu hỏi/rubric** đã có. Không hardcode secrets; `.env`, API keys, mật khẩu không được commit; tạo `.env.example` an toàn nếu cần.
- Không giả lập AI/STT/TTS thành “đã tích hợp”; nếu thiếu key, dùng cấu hình `NotConfigured` và thông báo chính xác. Không dùng browser `SpeechRecognition` / `webkitSpeechRecognition` làm con đường STT duy nhất (cần hoạt động trên Brave/Chrome); hỗ trợ nhập transcript thủ công **như phương án dự phòng**, ghi rõ hạn chế.
- Tránh kiến trúc 3 project 3-Layer, microservices, overengineering, copy-paste business logic; không đổi UI đang hoạt động nếu không cần.

## Thứ tự thực hiện — tự làm, không dừng sau mỗi giai đoạn

### A. Audit & kết nối MySQL (làm trước)
1. Xem có chọn đúng nhánh Git không; kiểm tra `git status`, thư mục `backend/`, frontend, API calls, `.csproj`, `Program.cs`, `appsettings*`, MySQL SQL dump/schema trong repo, migrations, `DbContext`, entities, `.env.example`, README. Dùng `rg`/liệt kê file chọn lọc, **không in toàn repo**.
2. Xác định DB trong repo là **file `.sql`/schema hay DB MySQL đang chạy**; không nhầm có file SQL với đã có kết nối MySQL. Kiểm tra MySQL host/port, connection string được cấu hình an toàn, `dotnet restore/build`, và **thử mở kết nối thực** bằng health check/query đọc đơn giản nếu có credentials. Nếu thiếu credentials/server, tiếp tục làm các phần không phụ thuộc và báo chính xác lệnh/thông tin tối thiểu cần tôi cung cấp; **không tự import SQL dump**.
3. Giữ nguyên MySQL; chọn EF Core provider **Pomelo.EntityFrameworkCore.MySql** đúng phiên bản tương thích EF Core/.NET, hoặc giữ provider hiện đang dùng nếu hoạt động. Tránh lỗi lệch version EF Core/Pomelo. Xác nhận quan hệ course–exam–students–questions–attempts từ schema thật rồi mới implement.

### B. Nhóm 2 — Exam & Schedule Management
Xây dựng **workflow thật chạy end-to-end**, không chỉ CRUD:
1. Giảng viên đã đăng nhập tạo kỳ thi: môn học, tên, mô tả (nếu hỗ trợ), thời điểm bắt đầu/kết thúc, thời lượng mỗi sinh viên, danh sách sinh viên, cấu hình **số câu chính** và **số câu hỏi đào sâu tối đa/thí sinh**, tùy chọn giới hạn **đào sâu mỗi câu chính**, chiến lược `Random`/`Adaptive`.
2. Backend validate: vai trò giảng viên/quyền trên môn học, start < end, slot > 0, lịch không trùng của sinh viên/giảng viên theo nghiệp vụ sẵn có, số sinh viên phù hợp tổng khung giờ, không vượt cấu hình ngân hàng câu hỏi; quy đổi múi giờ rõ ràng (UTC lưu DB nếu hợp schema, hiển thị Asia/Ho_Chi_Minh).
3. Tạo/cập nhật kỳ thi ở trạng thái nháp, gán sinh viên, phân/gợi ý slot có thứ tự, cho giảng viên xem trước, công bố lịch; tránh phá dữ liệu cũ. Phân quyền: chỉ giảng viên được quản lý; sinh viên chỉ xem lịch của mình.
4. Chọn câu chính cho mỗi sinh viên: dùng ngân hàng câu hỏi theo môn; `Random` lấy ngẫu nhiên có kiểm tra lịch sử phân câu, **hạn chế trùng câu giữa sinh viên thi liên tiếp** (ưu tiên chưa dùng trong nhóm gần nhất); `Adaptive` dùng tiêu chí năng lực/dữ liệu thực nếu đã có, nếu chưa thì áp dụng chiến lược có thể giải thích được và tài liệu hóa mức độ adaptive hiện tại. Nếu pool không đủ, trả cảnh báo hoặc fallback rõ ràng, không lặp vô hạn. **Persist assignment** để refresh/retry không đổi câu; xử lý race condition bằng transaction/unique constraints phù hợp schema.
5. Trạng thái phù hợp schema (ví dụ Draft → Published → InProgress → Completed/Cancelled); kiểm tra chuyển trạng thái hợp lệ.
6. API thống nhất với React hiện có cho tạo/sửa/xem kỳ thi, phân lịch, sinh viên, preview/publish, cấu hình và lịch của sinh viên. Không tự ý thay route cũ nếu frontend đang gọi được; bổ sung route có lý do rõ ràng.

### C. Nhóm 3 — AI Viva Interview Engine (bắt buộc chạy thực)
1. Sinh viên trong danh sách, đúng lịch/quyền truy cập mới được bắt đầu attempt; chặn start lặp, dùng **server time** để kiểm soát thời gian và trạng thái attempt. Tạo phiên thi và nạp câu chính đã phân từ Nhóm 2.
2. AI **đọc câu hỏi bằng TTS**, sinh viên **trả lời bằng microphone**, audio gửi lên backend để **STT gần thời gian thực**, hiển thị transcript và trạng thái kết nối. Dùng SignalR cho event/time updates/transcript nếu hợp lý; có timeout/retry/cancel, kiểm tra quyền và giới hạn kích thước/định dạng audio. Chọn MIME bằng `MediaRecorder.isTypeSupported`, không giả định Chrome/Brave giống nhau. Không đưa vendor secret xuống browser. Nếu provider không hỗ trợ streaming, dùng chunked upload/transcription với độ trễ công khai; không gọi nó là realtime tuyệt đối.
3. LLM nhận **câu chính + transcript câu trả lời + ngữ cảnh vừa đủ**, đánh giá thiếu ý/mơ hồ/mâu thuẫn và **sinh một câu hỏi follow-up liên quan trực tiếp** nếu cần. Không mặc định câu nào cũng hỏi xoáy; không làm lộ đáp án/rubric cho sinh viên. Log lưu rõ câu hỏi chính/follow-up và lời đáp.
4. Backend bắt buộc thực thi **2 mức giới hạn**: (a) tổng follow-up tối đa **mỗi sinh viên/attempt** từ Nhóm 2, (b) follow-up tối đa **mỗi câu chính**, cùng **thời gian trả lời từng lượt** và thời gian tổng phiên thi. Dù LLM trả sai/lặp hoặc client sửa request, backend vẫn không được vượt giới hạn. Khi hết lượt/timeout, chuyển câu chính tiếp theo hoặc hoàn tất.
5. Lưu bền vững attempt, câu hỏi, answers, transcript, timestamps, trạng thái, lỗi provider theo schema hiện có. Kết thúc đúng số câu chính hoặc deadline; hỗ trợ reconnect/refresh mà không làm mất attempt; tránh xử lý đồng thời hai câu trả lời cùng lượt.
6. Xử lý lỗi STT/TTS/LLM thật: timeout, rate limit, thiếu key, microphone denied, audio không được hỗ trợ. Retry giới hạn; fallback nhập văn bản để tiếp tục demo nhưng **phải báo đây là fallback**, không tuyên bố STT đạt yêu cầu khi chưa có provider.

### D. Tích hợp AI thực & hướng dẫn chỗ lấy key
1. Audit provider/code đã có trước. Nếu chưa có, **ưu tiên Gemini làm LLM mặc định** (hoặc Claude nếu project đã tích hợp ổn); tạo provider interface và adapter đơn giản, không tạo nhiều adapter rỗng. Đối với STT/TTS, chọn **một dịch vụ thật hỗ trợ tiếng Việt và có API/tài liệu chính thức** (ưu tiên tái sử dụng provider hiện có; nếu chưa có, cân nhắc Azure AI Speech cho STT + TTS để tránh phụ thuộc Web Speech theo browser). Phải xác minh SDK/API và các quyền cần thiết; không bịa endpoint.
2. Cấu hình **backend-only** bằng ASP.NET Core User Secrets khi local hoặc environment variables khi deploy. Đề xuất tên rõ ràng: `AI__Provider`, `AI__ApiKey` (Gemini/Claude); `Speech__Provider`, `Speech__Key`, `Speech__Region` (nếu chọn Azure); kết nối MySQL bằng `ConnectionStrings__DefaultConnection`. Tên cuối cùng phải khớp chính xác code đã implement; không hardcode.
3. Tạo tài liệu `docs/AI_KEYS_AND_SETUP.md` bằng **tiếng Việt**: link chính thức tạo API key (Gemini: https://aistudio.google.com/apikey ; Claude: https://console.anthropic.com/settings/keys ; Azure Speech nếu dùng: https://portal.azure.com/), dịch vụ nào cần key/region/billing, **vị trí dán key**, ví dụ lệnh `dotnet user-secrets set ... --project <đường_dẫn_csproj_thật>` hoặc env vars, các bước bật dịch vụ và test bằng endpoint thật. Không in/ghi log key; không yêu cầu đưa key vào chat; không ghi key vào `src/`.

### E. React ↔ Backend và trải nghiệm người dùng
1. Đọc các trang/components/API client React đang có; giữ style và luồng hiện tại. Hoàn thiện phần thiếu cho: **giảng viên tạo kỳ thi → chọn môn/sinh viên → cấu hình câu/lịch → preview/publish**, **sinh viên xem lịch → vào phòng chờ → bắt đầu thi → nghe câu hỏi → thu âm/STT → xem transcript → follow-up → hoàn thành**.
2. Nối API thật, token auth, role guard, CORS, base URL/Vite proxy, SignalR, validation, loading/error/empty states; bỏ mock data ở luồng đã có backend thật. Không xóa mock hoặc file không cần dùng nếu chưa được hỏi và đồng ý.
3. Nếu tính năng/route/model frontend và DB khác nhau, chọn hợp đồng API nhất quán và sửa cả hai đầu. Không để nút "hoạt động giả"; nếu provider chưa cấu hình, giao diện thông báo cụ thể.

### F. Kiểm thử & bàn giao
- Test tối thiểu: quyền Lecturer/Student; validate lịch; xung đột slot; tổng slot; chọn câu ít trùng; pool câu không đủ; start đúng/sai giờ; chống start/retry trùng; giới hạn số câu chính, **follow-up tổng & theo câu**, timeout server-side; STT/TTS/LLM failure; resume; persistence. Mock provider AI ở unit tests; **có ít nhất một bài test/manual test gọi provider thật khi có key**, không giả kết quả.
- Chạy restore/build backend, test hiện có, frontend `npm run build`, kiểm tra DB **thực** nếu có credentials, smoke test một flow giảng viên + sinh viên. Nếu không đủ môi trường để chạy, nêu rõ phần chưa kiểm chứng, không nói "đã chạy".
- Tạo `docs/AIVES_WORKFLOW_2_3.md` (luồng chi tiết, actor, states, API routes và ví dụ request/response), `docs/AI_KEYS_AND_SETUP.md`, `docs/RUN_AND_TEST.md` (lệnh copy/paste chạy MySQL, backend, React; tài khoản test nếu seed đã tồn tại; demo script và checklist).
- **Không tự push/merge Git.** Đề xuất các commit nhỏ theo nhóm việc và báo file thay đổi. Chỉ đề nghị xóa code legacy sau khi thay thế và hỏi tôi trước.

## Yêu cầu tiết kiệm token & cách báo cáo
- Tìm và đọc **file liên quan** bằng `rg`, `find`, `git status`, không dump nguyên file lớn hay toàn bộ repo; ưu tiên diff ngắn.
- Tự làm từ A → F, tự sửa lỗi build/test; **chỉ hỏi** khi thiếu quyết định nghiệp vụ không thể suy ra, thiếu thông tin truy cập cần thiết, hoặc trước thao tác xóa/phá hủy.
- Không tạo placeholder/fake success; tránh thiết kế dài dòng và thư viện không cần thiết.
- Phản hồi ngắn mỗi chặng: `Đã làm | Build/Test | Blocker` (tối đa khoảng 8 dòng). Cuối cùng báo: các tính năng đã chạy thật, phần chưa chạy, file tài liệu, cách cấu hình keys và **3 lệnh để khởi động dự án**.

**BẮT ĐẦU:** kiểm tra repo/DB trước, sau đó triển khai trọn Workflow 2 + 3 trên nền code hiện có, nối frontend/backend và kiểm thử. Không dừng ở việc lập kế hoạch.
