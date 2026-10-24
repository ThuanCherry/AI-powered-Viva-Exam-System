# System Admin — tích hợp và kiểm thử

Ngày kiểm tra: 24/10/2026.

## Kết quả tích hợp

- Đã fetch `origin/dev` và `origin/Luan`. `Luan` tại `3b7a778` đã là tổ tiên của `dev` tại `9fe7f98`; không có conflict merge mới.
- Sửa marker conflict đã bị commit trong `AIVES.slnx` / README. Solution dùng một backend `backend/AIVES.Api`, frontend React trong `src`.
- Giữ refactor Controller → Service → DbContext/EF Core → MySQL. AdminController cũng chỉ gọi service.

## Các lỗi đã sửa

- Tạo tài khoản thiếu API: thêm `POST /api/Admin/users`, kiểm tra tên/email/vai trò/mật khẩu, hash BCrypt và lưu database.
- Bỏ dữ liệu giả/localStorage fallback khi API tạo/sửa/khóa/phân công lỗi. Giao diện hiển thị lỗi từ server và giữ form để sửa.
- Backend bảo vệ tài khoản Admin khỏi khóa/hạ quyền; kiểm tra trạng thái, vai trò hợp lệ, chặn đổi vai trò khi còn liên kết môn/lớp/kỳ thi.
- JWT được đối chiếu tài khoản đang hoạt động và vai trò hiện tại trên mỗi HTTP request/SignalR handshake. Token có vai trò cũ hoặc tài khoản bị khóa bị từ chối.
- Phân công chỉ nhận giảng viên Active; giữ chặn chuyển môn đã có lớp/kỳ thi để không mất quyền nghiệp vụ.
- Việt hóa trang quản trị, thêm trạng thái lưu, ngày tạo, sửa CSS bị thiếu; ẩn trạng thái SignalR không áp dụng cho Admin.
- Tab cấu hình AI hiển thị cấu hình backend thật qua `GET /api/Admin/settings`, không lộ API key, không probe dịch vụ.

## Sử dụng

1. Chạy MySQL và backend/frontend theo README. Đăng nhập tài khoản có role `Admin` trong database hiện có, mở `/admin`.
2. **Tài khoản**: tạo mới với mật khẩu ít nhất 8 ký tự, tìm/lọc, sửa họ tên/vai trò, khóa/mở tài khoản. Email không đổi. Tài khoản đã gắn nghiệp vụ cần chuyển giao trước khi đổi vai trò.
3. **Phân công giảng viên**: chọn giảng viên đang hoạt động; tải lại trang để kiểm tra dữ liệu được lưu.
4. **Cấu hình AI**: chỉ đọc. Thay key/model tại `backend/AIVES.Api/appsettings.json` trên máy backend hoặc biến môi trường tương ứng rồi khởi động lại. Không cung cấp nút lưu giả hay nút đọc thử tự gọi TTS.

Không tạo tài khoản Admin mẫu trên MySQL thật. Database cần có các role `Admin`, `Lecturer`, `Student` như schema hiện tại. Không cần migration cho thay đổi này.

## Chạy kiểm thử độc lập

```bash
dotnet build AIVES.slnx --no-restore --artifacts-path artifacts/merge-build --verbosity quiet
dotnet test tests/AIVES.Workflow.Tests --artifacts-path artifacts/merge-build --no-restore --filter 'FullyQualifiedName~AdminWorkflowTests|FullyQualifiedName~LayeringTests|FullyQualifiedName~LecturerWorkflowTests|FullyQualifiedName~ScheduleTimingTests|FullyQualifiedName~RealtimeScheduleTests' --verbosity minimal
npm run build
npm run lint
```

Kết quả: **55/55 test đạt**, backend build 0 warning / 0 error, frontend build và lint đạt. Bộ HTTP test dùng SQLite in-memory, fake AI ném lỗi nếu bị gọi, chặn HTTP ngoại vi; luôn xác nhận AI/network call count bằng 0. Một lần chạy bị môi trường sandbox chặn kết nối testhost; lần chạy lại đã hoàn tất 55 test.

Đã kiểm thử bằng trình duyệt với backend sandbox 5240 và frontend 5174: đăng nhập Admin, tạo tài khoản, reload còn dữ liệu, sửa họ tên, khóa tài khoản, phân công giảng viên và reload, xem cấu hình AI. Không có request AI. Dữ liệu demo nằm trong bộ nhớ, mất khi dừng sandbox.

Có thể chạy lại sandbox bằng `dotnet run --project tools/LecturerDemoHost -- --ephemeral`; frontend trỏ `VITE_API_BASE_URL=http://localhost:5240`. Tài khoản test: `admin@lecturer-test.invalid`, mật khẩu `Lecturer-Sandbox-Only-123!`. Đây chỉ là tài khoản sandbox, không dùng cho môi trường thật.

Giới hạn: tab AI chỉ đọc cấu hình, không sửa cấu hình provider từ web; không kiểm chứng giọng nói/model/hạn mức thật. JWT kiểm tra lúc HTTP request/SignalR handshake, không chủ động ngắt một stream đã mở trước khi khóa tài khoản. Các project test 3-layer cũ không thuộc solution đang chạy.
