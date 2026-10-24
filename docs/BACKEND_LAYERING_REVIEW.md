# Review phân tầng backend — 24/10/2026

Luồng hiện tại: **React → Controller/Hub → Application Service → AivesDbContext (EF Core/Pomelo) → MySQL**. Các thành phần cùng nằm trong một backend Monolithic `backend/AIVES.Api`.

## Đã sửa

| HTTP / SignalR | Service xử lý |
|---|---|
| UsersController: đăng nhập/đăng ký | AuthService |
| UsersController: danh sách, cập nhật, khóa/mở | UserManagementService |
| CoursesController | CourseCatalogService |
| ClassesController | ClassQueryService |
| QuestionsController | QuestionQueryService |
| ExamsController và quyền JoinExam của ExamHub | ExamQueryService |
| LecturerController: dashboard, tìm kiếm, roster, tạo/sửa môn/lớp/câu hỏi, ghi danh | LecturerService |
| DocumentsController và upload tài liệu | DocumentStore |
| WorkflowController: catalog, sinh viên theo môn, tạo/sửa/công bố/hủy kỳ thi, lịch cá nhân | WorkflowService |
| WorkflowController: phiên thi | VivaEngine, giữ nghiệp vụ đang có |
| /health | DatabaseHealthService |

Controller nhận HTTP input, lấy identity từ JWT qua ApiControllerBase, gọi service và chuyển kết quả thành HTTP response. File/audio binding, HTTP header và gửi sự kiện SignalR thuộc lớp transport. Controller/Hub không giữ DbContext, không dùng EF/LINQ-to-database, không tự mở transaction hay SaveChanges.

Service chịu trách nhiệm truy vấn, kiểm tra quyền sở hữu, validation nghiệp vụ, mutation và transaction. `CancelAsync` giữ thứ tự khóa schedule → exam, commit trước khi thông báo. Các projection được lấy tại service; file download trả metadata DTO thay vì entity EF cho controller.

`Caller` được tạo từ JWT đã xác thực, không đọc userId/role từ request body. Role authorization vẫn ở endpoint; các thao tác nhạy cảm như quản lý tài khoản và phân công giảng viên có guard trong service. Lỗi ownership/not-found từ service được ExceptionMiddleware chuyển thành HTTP 403/404 với JSON thống nhất.

DI đăng ký chung tại `Features/ApplicationServices.cs`, dùng cùng danh sách service cho backend và host kiểm thử. Provider/worker và mock AI vẫn được đăng ký riêng theo môi trường.

## Kết quả review

- Không còn dependency AivesDbContext/EF Core trong cả 8 controller, lớp controller base và ExamHub.
- Không còn query/save/transaction tại controller. Health endpoint cũng gọi service.
- Không thay đổi URL API, dữ liệu response thành công, cấu hình AI, schema/MySQL hoặc frontend.
- Kiểm tra lại ownership, quyền quản trị, lọc sinh viên, bảo vệ phân câu, upload/download, khung giờ và thông báo sau commit.
- Không phát hiện lỗi nghiêm trọng mới trong phạm vi refactor sau các kiểm tra bên dưới.

## Kiểm thử

```bash
dotnet build AIVES.slnx --no-restore --artifacts-path artifacts/merge-build --verbosity quiet
dotnet test tests/AIVES.Workflow.Tests --artifacts-path artifacts/merge-build --no-restore --filter 'FullyQualifiedName~LayeringTests|FullyQualifiedName~LecturerWorkflowTests|FullyQualifiedName~ScheduleTimingTests|FullyQualifiedName~RealtimeScheduleTests' --verbosity quiet
```

LayeringTests kiểm tra dependency bằng reflection và quét implementation HTTP/Hub, đồng thời test contract/ownership truy vấn, quyền cập nhật tài khoản, phân công giảng viên, health service. Các bộ sẵn có kiểm tra toàn luồng giảng viên và SignalR/WebSocket thực trên SQLite in-memory.

Không gọi Gemini/STT/TTS, không mở server với cấu hình production, không ghi dữ liệu MySQL thật. Kết quả giới hạn ở build và bộ test ứng dụng Monolithic được chọn, không phải kiểm thử AI thật hoặc các project test 3-layer cũ.

Kết quả lần chạy cuối: **build đạt, 0 warning / 0 error; 47/47 test đạt**. Hai file `AIVES.slnx` và `README.md` có sửa từ lượt trước được giữ nguyên trạng thái, không reset hoặc ghi đè trong lần refactor này. Chưa commit/push.

Cập nhật 24/10/2026: refactor đã được commit trong `5f9e85f`; tích hợp Admin bổ sung controller/service và kiểm thử quyền tài khoản hiện tại. Xem `SYSTEM_ADMIN_REVIEW.md` cho kết quả mới 55/55 test và giới hạn kiểm thử.
