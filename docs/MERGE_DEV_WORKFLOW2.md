# Tích hợp dev và kiểm thử workflow 2 — 21/10/2026

Nguồn: `origin/dev` tại `bd2e5d5`, tích hợp vào nhánh workflow trước khi đưa kết quả về `dev` local. Không push GitHub.

| Xung đột / phần so sánh | Quyết định |
|---|---|
| API giảng viên / lịch thi | Giữ WorkflowService: xác thực JWT, quyền sở hữu lớp, chống trùng, transaction, tự tính kết thúc, giữ assignment và SignalR. Không dùng CRUD ExamService cũ nhận lecturerId từ client và xóa candidate để phân lại. File legacy vẫn được giữ, không đăng ký/routing vào runtime. |
| React giảng viên | Giữ dashboard Việt hóa và luồng môn → lớp → sinh viên → tài liệu/câu hỏi → lịch. Không thay bằng các trang có dữ liệu mô phỏng hoặc cần nhập kết thúc thủ công. |
| Bảng phân câu từ dev | Tích hợp vào chi tiết kỳ thi, chuyển sang dữ liệu đã lưu qua API. Hiển thị câu chính theo sinh viên, số câu riêng và số trùng liền kề; không giả lập người/câu hỏi. API chỉ trả phân câu cho chủ kỳ thi, không lộ cho Student. |
| Authentication / vai trò | Giữ token mapping và SignalR hiện tại, kết hợp home theo Admin/Lecturer/Student, hỗ trợ cả roles/allowedRoles. Giữ màn hình Admin có sẵn, bổ sung kiểm tra quyền cho endpoint dev để không cho người dùng khác nâng vai trò / khóa tài khoản / đổi giảng viên. Không phát triển thêm Admin. |
| SQL / cấu hình | Giữ schema và dữ liệu MySQL; không chạy dump, migration hoặc seed. Giữ appsettings local của người dùng, không đưa secret vào commit. Không bật LLM khi nộp. |
| File cũ | Khôi phục DistributePreview để tránh mất file khi merge. Các trang legacy chưa nối route vẫn giữ mã nguồn; luồng chính chỉ dùng API đã kiểm tra. |

## Kiểm tra

- Backend build: 0 warning / 0 error.
- 45 ca backend: toàn bộ LecturerWorkflowTests, ScheduleTimingTests, RealtimeScheduleTests và 4 ca validation/sửa nháp liên quan. Bao gồm API full workflow, CSV, PDF/DOCX và quyền tải file, quyền giảng viên/sinh viên, chống lộ phân câu, trùng lịch, qua ngày, chỉnh lịch đã công bố, SignalR và CORS.
- 16 ca frontend: tính lịch, validation và reconnect/đồng bộ sự kiện. Lint và production build đạt.
- Browser hai tab: login từng vai trò → dashboard thật → kỳ thi đã công bố → lưu tên/thời lượng → Student tự cập nhật không reload → giảng viên xem câu hỏi đã phân từ database.
- Bằng chứng trong `artifacts/merge-dev/student-signalr.png` và `lecturer-assignments.png` (gitignored).
- API sandbox cuối lượt trả `aiCalls: 0`, `externalHttpCalls: 0`, `productionConfigurationLoaded: false`.

## Dữ liệu kiểm thử và dọn dẹp

API integration dùng SQLite in-memory; tài khoản/lớp/câu hỏi/lịch mất khi đóng connection. Thư mục upload sinh GUID được kiểm tra phạm vi và tự xóa trong DisposeAsync. Browser dùng `LecturerDemoHost --ephemeral`, không dùng MySQL hoặc SQLite preview có sẵn; đóng host là hủy dữ liệu. Server kiểm thử đã dừng, không xóa preview/history cũ hoặc dữ liệu người dùng.

```bash
dotnet test tests/AIVES.Workflow.Tests --artifacts-path artifacts/merge-build --filter 'FullyQualifiedName~LecturerWorkflowTests|FullyQualifiedName~ScheduleTimingTests|FullyQualifiedName~RealtimeScheduleTests|FullyQualifiedName~ValidateRejectsInvalidSlots|FullyQualifiedName~DraftEditPreservesLegacy' --verbosity quiet
node --test tests/exam-schedule.test.mjs tests/exam-validation.test.mjs tests/schedule-realtime.test.mjs
npm run lint
npm run build
```

Không chạy test provider/voice thật. Các ca cũ về giờ kết thúc thủ công đã được đổi sang validation slot tự tính; ca sửa nháp dùng giờ tương lai theo khóa lịch hiện tại.

## Hoàn tất trên dev local
Hai commit dev riêng (`68ae7b6`, `26584ba`) được bảo toàn khi merge; không reset nhánh dev theo remote. `.gitignore` kết hợp quy tắc hai phía, README chọn hướng dẫn React + monolith hiện hành và giữ ghi chú các project 3-layer cũ. Các project cũ và sơ đồ cũ được giữ file, không đưa vào luồng frontend. Nhánh nguồn chỉ được xóa sau khi merge thành công vào dev.

Cập nhật sau merge theo yêu cầu: đã xóa ba thư mục project 3-layer trong `src/` và sửa `AIVES.slnx` sang backend Monolithic. Nội dung trên mô tả quyết định tại thời điểm merge; lịch sử các project vẫn có trong Git.
