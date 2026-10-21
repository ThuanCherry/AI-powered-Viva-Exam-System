# Workflow giảng viên AIVES

## Luồng sử dụng

`Đăng nhập Lecturer → Môn học → Lớp học phần → Ghi danh → Tài liệu/câu hỏi → Xem trước lịch → Lưu nháp → Công bố → Student xem lịch`

1. **Môn học:** chọn môn đang phụ trách hoặc tạo môn; mã không trùng, có thể sửa tên/mô tả. Backend lấy giảng viên từ JWT.
2. **Lớp học phần:** chọn môn, tạo mã/tên lớp. Mỗi lớp có danh sách sinh viên riêng; có thể sửa tên/mã nhưng không chuyển môn của lớp đã tạo.
3. **Sinh viên:** vào lớp, tìm tên/email tài khoản Student đang hoạt động, ghi danh từng người hoặc nhập CSV. Schema hiện chưa có mã sinh viên, học kỳ/năm học; không thêm trường giả.
4. **CSV:** cột duy nhất `email`, tối đa 100KB/500 dòng dữ liệu; dùng [mẫu CSV](templates/enrollment.csv). Thay email mẫu bằng email Student đã có. Sai email, tài khoản không hoạt động hoặc dòng trùng sẽ báo số dòng và không ghi một phần. Ghi danh đã có được bỏ qua, không tạo tài khoản mới.
5. **Tài liệu:** chọn môn, tải PDF/DOCX tối đa 10MB, xem danh sách/tải xuống/gỡ. File lưu riêng, kiểm tra nội dung và tên an toàn. Upload chỉ lưu file + metadata, không trích xuất hay gọi AI.
6. **Ngân hàng câu hỏi:** nhập câu chính, đáp án tham khảo, độ khó, mức Bloom; lưu Nháp hoặc Sẵn sàng. Chỉ câu chính Approved được phân vào đề. Có tìm kiếm, phân trang, chỉnh sửa, lưu trữ. Câu đã phân vào đề không được sửa nội dung để giữ lịch sử.
7. **Kỳ thi:** từ lớp chọn “Tạo kỳ thi của lớp”; môn/lớp được điền sẵn. Chọn một phần/tất cả sinh viên, ngày bắt đầu trong lịch tháng tùy chỉnh, giờ/phút và thời lượng mỗi sinh viên. Không nhập giờ kết thúc: backend tự tính `bắt đầu + số SV × phút/SV`, frontend hiển thị chỉ đọc. Bảng preview có ngày/giờ bắt đầu và kết thúc từng SV, tự cập nhật khi đổi lựa chọn, kể cả qua nửa đêm/tháng/năm. Thứ tự chọn là thứ tự slot; bỏ chọn/chọn lại để chuyển xuống cuối. Giữ cấu hình câu hỏi/chiến lược hiện có, báo đủ/thiếu câu trước khi ghi. Chỉ sửa bản nháp chưa tới giờ bắt đầu; lịch đã công bố/đã bắt đầu giữ nguyên.
8. **Lưu/công bố:** xác nhận lưu nháp để backend kiểm tra roster, thời lượng, pool, trùng lịch sinh viên/giảng viên/lớp. Kiểm tra lịch rồi xác nhận công bố bản nháp đã lưu. Random/Adaptive chỉ phân câu từ ngân hàng, không gọi LLM. Hủy cần xác nhận; không xóa lịch sử thi.
9. **Sinh viên:** đăng nhập Student, chỉ thấy lớp đã ghi danh và lịch của mình đã được công bố. Demo giảng viên kết thúc ở màn hình lịch; không cần mở phòng vấn đáp.

Dashboard/menu giảng viên dùng tiếng Việt, số liệu API thật: môn/lớp, số sinh viên phân biệt, câu hỏi/tài liệu, kỳ thi sắp tới; có lối tắt đến các bước trên.

## Dữ liệu và quyền

Không thêm bảng, không chạy migration/DDL hoặc seed vào MySQL thật. Tái sử dụng:

| Bảng | Vai trò |
|---|---|
| `users`, `roles` | JWT/role có sẵn; tìm Student đang hoạt động |
| `courses`, `academic_classes` | Môn và lớp, kiểm tra chủ sở hữu ở backend |
| `class_students` | Ghi danh theo khóa lớp + sinh viên |
| `documents`, `questions` | Tài liệu và ngân hàng **dùng chung theo môn** |
| `exam_sessions`, `exam_candidates` | Lịch thi và slot từng sinh viên |
| `exam_viva_settings`, `exam_questions` | Cấu hình giới hạn và phân câu cố định |

ClassId được lưu thêm vào JSON `exam_sessions.question_scope_json` đang có, không thêm cột/FK. Backend kiểm tra lớp thuộc đúng môn và đúng giảng viên. Lịch cũ không có ClassId vẫn đọc/sửa theo nghiệp vụ cũ; lịch mới bắt buộc lớp và không cho xóa liên kết lớp khi chỉnh sửa. Vì JSON chưa có FK, không chỉnh tay JSON hoặc xóa lớp trực tiếp trong DB.

Schema hiện có một giảng viên phụ trách môn. Chỉ chủ sở hữu môn và lớp được quản lý; không nhận lecturerId từ client. Tài liệu/câu hỏi được chia sẻ cho các lớp của môn, không giả lập kho riêng từng lớp. Student chỉ tải tài liệu môn đã ghi danh. Admin không được thêm vào các endpoint quản trị giảng viên mới.

Gỡ ghi danh chỉ bỏ quan hệ lớp–SV; chặn khi còn lịch/nháp chưa kết thúc của lớp. Với lịch cũ chưa gắn lớp, chặn thận trọng nếu còn lịch cùng môn. Gỡ tài liệu/câu hỏi đặt Archived, giữ hàng dữ liệu/file và lịch sử; không xóa file vật lý.

## API chính

Các API trả JSON `{ success, data }`; lỗi `{ success: false, message }`, JWT Bearer. Upload multipart và download nhị phân là ngoại lệ. POST/PUT/DELETE dưới `/api/lecturer` chỉ dành cho Lecturer.

| API | Chức năng |
|---|---|
| `GET /api/lecturer/dashboard` | Thống kê và 5 kỳ thi sắp tới |
| `GET /api/Courses`; `GET /api/Courses/{id}` | Môn của mình / môn đã ghi danh |
| `POST /api/lecturer/courses`; `PUT /api/lecturer/courses/{id}` | Tạo/sửa môn |
| `GET /api/classes?courseId=...` | Lớp theo quyền, lọc môn tùy chọn |
| `POST /api/lecturer/classes`; `PUT /api/lecturer/classes/{id}` | Tạo/sửa lớp |
| `GET /api/lecturer/students/search?q=...&page=1` | Tìm Student; tối thiểu 2 ký tự, 30/trang |
| `GET /api/lecturer/classes/{id}/students` | Roster lớp sở hữu |
| `POST/DELETE /api/lecturer/classes/{id}/students/{studentId}` | Ghi danh/gỡ |
| `POST /api/lecturer/classes/{id}/students/import` | Body `{ "csv": "email\n..." }`, trả added/alreadyEnrolled/errors |
| `GET /api/documents?courseId=...` | Metadata theo quyền |
| `POST /api/lecturer/courses/{id}/documents` | Multipart field `file` |
| `GET /api/documents/{id}/download` | File riêng tư có kiểm tra quyền |
| `DELETE /api/lecturer/documents/{id}` | Lưu trữ, giữ file |
| `GET /api/Questions?courseId=...&q=...&status=...&page=1&pageSize=20` | Câu hỏi, tối đa 100/trang |
| `POST /api/lecturer/questions`; `PUT/DELETE /api/lecturer/questions/{id}` | Câu thủ công / lưu trữ |
| `GET /api/workflow/catalog` | Môn và số câu chính Sẵn sàng |
| `GET /api/Exams?classId=...`; `GET /api/Exams/{id}` | Lịch theo lớp / cấu hình và preview |
| `POST /api/Exams`; `PUT /api/Exams/{id}` | Lưu nháp, có `classId` |
| `POST /api/Exams/{id}/publish`; `POST /api/Exams/{id}/cancel` | Công bố / hủy |
| `GET /api/schedule/mine` | Lịch đã công bố của Student hiện tại |

401: chưa đăng nhập; 403: sai role/chủ sở hữu; 400: cấu hình không hợp lệ; 409: trùng/ràng buộc; 413/415: file quá lớn/sai loại. Không trả secret hay đường dẫn vật lý cho client.

## Chạy trên backend/MySQL hiện có

Yêu cầu .NET 10 SDK, Node/npm, MySQL 8 và schema AIVES đã có. Lượt triển khai đã xác minh kết nối **MySQL 8.0.46, aives_db bằng SELECT**, đọc cấu trúc và số lượng; không ghi dữ liệu thật. Các kịch bản ghi mới được test trong SQLite test riêng, chưa chạy ghi end-to-end trên MySQL thật.

Backend đọc `ConnectionStrings:DefaultConnection`, `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` qua cấu hình ASP.NET; môi trường Development hỗ trợ User Secrets id `AIVES-Workflow-Local`. Giữ credentials trong User Secrets hoặc biến môi trường `ConnectionStrings__DefaultConnection`, `Jwt__Key`; không đặt vào React, tài liệu hoặc Git. Không import dump/reset DB; dùng tài khoản MySQL quyền tối thiểu, không cần quyền DROP/ALTER cho phần bổ sung này.

Kho file mặc định: `backend/AIVES.Api/App_Data/Documents`, ngoài public/wwwroot. Có thể đặt biến `Documents__StorageRoot` trỏ đến thư mục riêng có quyền đọc/ghi cho process backend. Sao lưu đồng bộ metadata DB + thư mục file. Tài liệu legacy chưa có tên file UUID trong kho này được trả 404, không tự di chuyển/xóa dữ liệu cũ.

Chạy từ root trong hai terminal:

```powershell
dotnet run --project backend/AIVES.Api
```

```powershell
npm ci
npm run dev
```

Frontend thường ở `http://localhost:5173`; `VITE_API_BASE_URL` trỏ backend (cấu hình hiện có `http://localhost:5234`). Dùng tài khoản Lecturer/Student hiện có. Không seed tài khoản demo vào DB thật. Phần giảng viên không cần API key AI; ứng dụng chính vẫn chứa luồng vấn đáp cũ. Để demo bảo đảm không gọi bất kỳ AI/ngoại vi nào, dùng sandbox dưới đây.

## Demo an toàn, không gọi AI

Sandbox tái sử dụng controller/service/auth thật, chỉ đổi database và tích hợp ngoại vi. Nó xóa nguồn cấu hình mặc định, không nạp User Secrets/cấu hình sản xuất, không có worker thi/hub Live. AI mock và mọi HttpClient ngoại vi đều bị chặn. Không start backend MySQL để demo này.

Terminal 1, từ root:

```powershell
dotnet run --project tools/LecturerDemoHost
```

Terminal 2:

```powershell
$env:VITE_API_BASE_URL='http://localhost:5240'
npm run dev -- --port 5174 --strictPort --host 127.0.0.1
```

Mở `http://localhost:5174`. Biến VITE chỉ áp dụng process terminal này; không sửa `.env` đang có. Database test lưu tại `artifacts/lecturer-sandbox/preview.sqlite`; file tại `artifacts/lecturer-sandbox/preview/documents`. Không trỏ tool này vào MySQL. Thư mục artifacts bị Git ignore.

| Role | Email sandbox |
|---|---|
| Lecturer | `lecturer@lecturer-test.invalid` |
| Student | `student1@lecturer-test.invalid` |
| Student | `student2@lecturer-test.invalid` |
| Lecturer khác (thử quyền) | `other@lecturer-test.invalid` |

Mật khẩu chung **chỉ cho dữ liệu sandbox**: `Lecturer-Sandbox-Only-123!`. Khóa JWT thử nằm trong harness, không dùng cho production. Sandbox chỉ phục vụ localhost, không triển khai ra Internet.

Kịch bản demo: tạo môn → tạo 2 lớp cùng môn → ghi danh Student1 lớp A, Student2 lớp B → tải PDF/DOCX → soạn ít nhất 2 câu Approved → tạo kỳ thi lớp A với 2 câu → kiểm tra chỉ roster A, xem preview → lưu nháp → công bố → đăng nhập Student1 xem lịch → Student2 không thấy lịch lớp A. Không bấm vào phòng vấn đáp. `GET http://localhost:5240/sandbox/status` hiển thị database test, `aiCalls: 0`, `externalHttpCalls: 0`, `productionConfigurationLoaded: false`.

Đã chạy trình duyệt thực với môn SWD392/lớp SE-K19-01, ghi danh Student1, câu thủ công, preview/lưu/công bố kỳ thi và Student1 thấy đúng lịch. File upload/download, CSV, quyền và các trường hợp lỗi được kiểm tra qua HTTP trong bộ test bên dưới.

## Kiểm thử đúng phạm vi

```powershell
dotnet build backend/AIVES.Api/AIVES.Api.csproj --no-restore
dotnet test tests/AIVES.Workflow.Tests/AIVES.Workflow.Tests.csproj --filter FullyQualifiedName~LecturerWorkflowTests --verbosity quiet
npm run lint
npm run build
```

10 test HTTP dùng Kestrel/JWT thật + SQLite riêng: luồng đầy đủ gồm upload/lịch Student; ownership/role/lớp; mã/validation; CSV nguyên tử/idempotent; PDF/DOCX/size/private/archive; câu đã dùng; lớp/khung giờ/pool/trùng lịch; trùng slot xuyên giảng viên; CSV sai role/inactive/duplicate; chống traversal và catalog riêng tư. AI/ngoại vi phải có số lần gọi bằng 0. Không chạy toàn bộ suite, ProviderProbe, WorkflowSmoke hay Gemini E2E trong lượt kiểm thử giảng viên.

Kết quả: backend và frontend build đạt, lint đạt, 10/10 test chọn lọc đạt. Không kiểm thử ghi vào MySQL thật; chưa kiểm thử tải nhiều người đồng thời. Không thêm Admin. Luồng AI giữ nguyên; thay đổi tương thích duy nhất ở công cụ smoke/E2E cũ là bổ sung classId cho hợp đồng tạo kỳ thi mới, các công cụ đó không được thực thi.

## Cập nhật tự tính thời gian lịch thi

- POST/PUT `/api/Exams` không cần `endTime`. Client cũ vẫn gửi được trường này, nhưng backend bỏ qua giá trị khi lưu và tính lại từ StartTime, TimePerStudent, StudentIds. Trả EndTime thực tế qua GET chi tiết; từng candidate có `scheduledTime` và `scheduledEndTime`.
- Lưu UTC trong các cột StartTime/EndTime/ScheduledTime hiện có. Kết thúc slot = ScheduledTime + TimePerStudent, không thêm cột/migration; khoảng thời gian liền kề không tính là trùng.
- Lịch đã lưu không bị tự chuyển đổi khi đọc/công bố; bản nháp chưa bắt đầu chỉ được tính lại khi người dùng lưu. Backend chặn thay đổi lịch đã tới giờ bắt đầu hoặc đã có SV bắt đầu/hoàn thành.
- Kiểm thử riêng cập nhật này: `dotnet test tests/AIVES.Workflow.Tests/AIVES.Workflow.Tests.csproj --artifacts-path artifacts/schedule-build --filter FullyQualifiedName~ScheduleTimingTests --verbosity quiet` và `node --test tests/exam-schedule.test.mjs`. Có 19 ca backend/HTTP và 6 ca preview JS, chỉ dùng SQLite test/mock AI; không gọi provider, không ghi MySQL thật.
- Nếu backend đang chạy khóa file exe, dùng `dotnet build backend/AIVES.Api/AIVES.Api.csproj --artifacts-path artifacts/schedule-build` để build vào thư mục riêng. Dừng backend bằng Ctrl+C rồi chạy lại để nạp code mới khi demo thật.
- Nút lưu chỉ khóa trong lúc gửi yêu cầu. Thiếu ngày/sinh viên hoặc thiếu câu Approved sẽ hiện hướng dẫn ngay trên nút; bấm lưu sẽ báo lý do cụ thể. Con trỏ chờ chỉ xuất hiện khi đang lưu thật, yêu cầu lưu có giới hạn 20 giây. Nếu hết thời gian chờ, kiểm tra danh sách kỳ thi trước khi thử lại để tránh tạo trùng. Kiểm tra frontend: `node --test tests/exam-validation.test.mjs tests/exam-schedule.test.mjs` (11 ca).
- Form tạo mới gợi ý giờ bắt đầu sau ít nhất 1 phút (làm tròn lên theo phút). Nút `+1 phút` cập nhật lại gợi ý theo thời điểm bấm; lịch đã lưu không tự đổi. Nhãn kết thúc được rút gọn thành “Kết thúc dự kiến”, không hiển thị công thức nhân số sinh viên. Frontend gửi thêm endTime đã tính để tương thích backend cũ; backend mới vẫn tự tính và không tin endTime client. Sau khi sửa backend phải dừng tiến trình cũ và chạy lại, nếu không có thể vẫn gặp thông báo kiểm tra start/end từ bản cũ.

## File chính

### SignalR và chỉnh sửa lịch đã công bố

- Có nhãn **SignalR · Tự đồng bộ** trên giao diện. Backend phát `ScheduleChanged` sau khi transaction thành công tới nhóm người dùng lấy từ JWT, gồm giảng viên và sinh viên cũ/mới liên quan; không broadcast toàn hệ thống, không chứa đề/đáp án/transcript.
- Tạo nháp chỉ thông báo giảng viên. Công bố, sửa lịch, hủy, bắt đầu/nộp lượt, sửa môn/lớp và ghi danh sẽ làm các màn hình liên quan tự lấy lại API. Khi nối lại hoặc quay lại tab cũng đồng bộ; mất SignalR có REST dự phòng mỗi 15 giây. Khi kết nối vẫn báo hoạt động, cũng đối chiếu dữ liệu mỗi 15 giây để bù sự kiện bị bỏ lỡ. Quay lại tab hoặc có mạng trở lại sẽ đồng bộ ngay. Không reload nguyên trang hay xóa transcript đang nhập.
- Lịch `Scheduled` **chưa tới giờ bắt đầu và chưa có ai thi** được sửa tên/mô tả, giờ, thời lượng, sinh viên và giới hạn hỏi/trả lời. Môn/lớp, số câu chính, chiến lược đã phân giữ cố định. Xác nhận ngay trong form trước khi cập nhật lịch công bố.
- Giữ nguyên assignment sinh viên cũ; sinh viên mới được phân câu từ ngân hàng, người bị gỡ được đánh dấu Cancelled, không xóa câu/lịch sử. Kiểm tra trùng lịch như khi tạo; khóa hàng kỳ thi MySQL đồng bộ với thao tác bắt đầu thi. Kỳ thi đã bắt đầu không được sửa.
- TTS chỉ chạy khi người dùng bấm **Nghe câu hỏi**; tải trang, nối lại SignalR hoặc thay đổi trạng thái cấu hình không tự gọi TTS. STT/LLM không được gọi trong kiểm thử này.
- Demo hai tab: sandbox 5240 + frontend 5174, login Lecturer ở một tab và Student1 ở tab khác (không chọn Remember me). Mở lịch Student1; Lecturer sửa kỳ thi đã công bố trong tương lai và xác nhận. Tên/giờ/thời lượng Student1 tự đổi, không refresh. Sandbox dùng kết nối SQLite riêng cho từng request trình duyệt, không nạp MySQL/keys thật.
- Kiểm thử: `dotnet test tests/AIVES.Workflow.Tests --artifacts-path artifacts/realtime-build --filter "FullyQualifiedName~RealtimeScheduleTests|FullyQualifiedName~ScheduleTimingTests" --verbosity quiet`. 27 ca gồm SignalR/WebSocket thật, CORS negotiate có xác thực, người nhận/ngoài lớp, reconnect, giữ assignment, gỡ/ghi danh, trùng lịch và khóa lịch đã bắt đầu; AI/HTTP ngoại vi đều 0 lần gọi.

- Backend: `Controllers/LecturerController.cs`, `Controllers/ClassesController.cs`, `Features/Lecturer/*`; bổ sung scope ở Courses/Questions/Exams và WorkflowService/WorkflowRules.
- React: `pages/LecturerDashboard.jsx`, `pages/Teaching.jsx`, `pages/TeachingResources.jsx`, `pages/Exams.jsx`, `hooks/useApi.js`, `styles/Lecturer.css`, router/sidebar/client.
- Kiểm thử: `tests/AIVES.Workflow.Tests/LecturerWorkflowTests.cs`, `tests/LecturerHarness/LecturerSandbox.cs`, `tools/LecturerDemoHost`.
- Các trang mock cũ vẫn giữ file; router các màn hình giảng viên nay dùng trang kết nối API thật. Không xóa nghiệp vụ cũ, không push/merge Git.

### Sửa đồng bộ lịch ngày 19/10/2026
- CORS Development dùng chung cho ứng dụng và test, hỗ trợ localhost/127.0.0.1 ở cả 5173 và 5174. Production chỉ dùng danh sách cấu hình.
- SignalR cập nhật ngay khi nhận ScheduleChanged; tự nối lại, lấy dữ liệu mới khi có mạng/quay lại tab và đối chiếu 15 giây để bù sự kiện thất lạc. Không tải lại toàn trang.
- Sau khi lấy code mới: khởi động lại backend/frontend, tải lại trang sinh viên một lần để nạp JavaScript mới. Các lần lưu lịch sau đó tự cập nhật. Test hai vai trò bằng hai trình duyệt hoặc đăng nhập không chọn ghi nhớ để tránh dùng chung phiên localStorage.
- Kiểm tra lần này: 31 ca backend (SignalR, CORS, thời gian lịch) + 5 ca frontend lifecycle/reconnect đều pass; lint và build frontend pass. Không gọi AI, không ghi dữ liệu MySQL thật.
- Test frontend: `node --test tests/schedule-realtime.test.mjs`.
