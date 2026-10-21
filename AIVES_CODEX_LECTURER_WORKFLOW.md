# AIVES — Codex + MCP | Lecturer Workflow (tiết kiệm token)

Bạn là Senior Full-stack Engineer, chuyên C# ASP.NET Core Monolithic, EF Core/MySQL, React và SWD. Làm trực tiếp trong repository đang mở. **Triển khai code chạy được**, không chỉ lập kế hoạch hay tạo skeleton.

## Hiện trạng & phạm vi
- Backend: `backend/`, C# ASP.NET Core **Monolithic** (một ứng dụng triển khai), MySQL đã có schema/cấu hình nhưng tình trạng kết nối cần xác minh.
- Frontend: React/Vite dùng JavaScript JSX, source tại `src/` ở root; giữ cấu trúc hiện có, **không ép chuyển TypeScript**.
- Luồng cần hoàn thiện: **Giảng viên → Môn học/lớp học phần → Ghi danh sinh viên → Tài liệu/ngân hàng câu hỏi → Tạo kỳ thi & lịch thi → Sinh viên xem lịch**.
- **KHÔNG làm Admin ở lượt này. KHÔNG triển khai, sửa hoặc gọi Gemini/Claude/STT/TTS/AI Interview** trừ khi thay đổi nhỏ bắt buộc để tương thích, và phải giải thích.

## Quy trình làm việc
1. Đọc cấu trúc repo, model/entity, DbContext, migration/SQL, authentication/role, API hiện có, giao diện React và Git diff. Tận dụng code sẵn; tránh trùng chức năng/bảng/API.
2. Kiểm tra cấu hình MySQL và thử **kết nối an toàn, chỉ đọc** nếu môi trường có credentials; nếu không kết nối được, báo rõ nguyên nhân và tiếp tục những phần không cần DB thật. Không tự tạo/reset DB sản xuất.
3. Lập checklist NGẮN các mục còn thiếu và thực hiện tuần tự. Mỗi phần xong tự build/kiểm tra có liên quan.

## Yêu cầu nghiệp vụ (chỉ thêm phần chưa có)
### A. Môn học và lớp học phần
- Giảng viên xem/tạo/chỉnh sửa môn học **nếu nghiệp vụ/schema cho phép**, tránh trùng mã môn; ưu tiên chọn môn có sẵn. Phân biệt **Course (môn)** với **Section/Class (lớp học phần)** để không lẫn sinh viên các lớp cùng môn.
- Giảng viên tạo/quản lý lớp mình phụ trách (mã lớp, môn, học kỳ/năm học nếu có), xem danh sách lớp. Nếu schema đã có quan hệ tương đương thì tái sử dụng.
- Chỉ giảng viên được phân quyền với lớp mới được thay đổi sinh viên, tài liệu, câu hỏi và kỳ thi của lớp đó; không tin `lecturerId` từ client.

### B. Ghi danh sinh viên
- Chọn sinh viên từ **tài khoản Student hiện có** để gán vào lớp; tìm theo mã SV/email/tên; chống ghi danh trùng; cho xem/xóa ghi danh với kiểm tra các ràng buộc lịch thi đang tồn tại.
- Nếu phù hợp, thêm import CSV để ghi danh hàng loạt: kiểm tra mã SV/email, báo dòng lỗi, không tạo tài khoản Admin, không tự tạo tài khoản giả; mẫu CSV đơn giản.
- Sinh viên chỉ thấy các lớp và kỳ thi mà mình thuộc về.

### C. Tài liệu và ngân hàng câu hỏi
- Giảng viên upload/xem/tải/xóa tài liệu lớp/môn (PDF/DOCX theo khả năng hiện có); giới hạn dung lượng, kiểm tra kiểu file, tên file lưu an toàn, **không để file riêng tư trong `public/`**, kiểm tra quyền tải/xóa. Lưu metadata vào MySQL; không đưa tài liệu cho Gemini.
- Tái sử dụng ngân hàng câu hỏi sẵn có. Nếu thiếu, bổ sung CRUD câu hỏi thủ công gắn đúng môn/lớp, nội dung, độ khó/chủ đề nếu đã có schema. Không tạo câu hỏi tự động bằng AI.
- Thể hiện số câu đủ/thiếu trước khi tạo kỳ thi.

### D. Kỳ thi và lịch thi (nhóm chức năng 2)
- Giảng viên chọn **môn → lớp → danh sách SV đã ghi danh** (mặc định chỉ SV thuộc lớp; cho chọn một phần hoặc tất cả).
- Cấu hình tên kỳ thi, ngày/giờ bắt đầu-kết thúc, thời lượng mỗi SV, thứ tự/khung giờ, số câu hỏi chính, số câu hỏi đào sâu tối đa mỗi SV, giới hạn hỏi đào sâu mỗi câu (nếu schema/flow hiện có), chiến lược `Random`/`Adaptive`.
- Validate: đầu-cuối hợp lệ, thời lượng dương, đủ khung giờ cho SV, số câu hợp lệ, không trùng lịch cùng SV/lớp nếu có quy tắc, SV chỉ thuộc lớp, ngân hàng câu hỏi đủ theo chính sách hiện tại.
- Sinh slot thi, cho giảng viên xem trước danh sách SV + khung giờ; chỉ ghi khi xác nhận. Trạng thái nháp/công bố nếu hệ thống đã có; không phá trạng thái kỳ thi cũ.
- `Adaptive` chỉ cấu hình/lưu, **KHÔNG gọi LLM** trong bước này. Giữ nguyên phần AI phỏng vấn cũ để tích hợp sau.
- Sinh viên xem lịch đã công bố của mình; giảng viên xem danh sách kỳ thi của lớp mình.

### E. Frontend React — toàn bộ dashboard tiếng Việt
- Kiểm tra trang đã có, sửa/thêm **chỉ những trang thực sự cần thiết** để hoàn chỉnh luồng giảng viên.
- Dashboard giảng viên tiếng Việt, gọn gàng, responsive: tóm tắt **môn/lớp đang dạy, số SV, kỳ thi sắp tới, số câu hỏi/tài liệu** nếu dữ liệu có; thao tác nhanh: `Quản lý lớp`, `Sinh viên`, `Tài liệu`, `Ngân hàng câu hỏi`, `Tạo kỳ thi`.
- Tạo/chỉnh các màn hình theo flow; menu nhất quán; form có validation, trạng thái loading/empty/error/success, xác nhận hành động ảnh hưởng dữ liệu; không để nút giả.
- UI dùng tiếng Việt nhất quán (nhãn, thông báo, lỗi); tránh dashboard nhiều chart/card không giá trị, animation dư, giao diện rườm rà.
- Kết nối API backend thực; giữ auth, client hiện có và component có thể tái sử dụng. Không làm lại toàn bộ giao diện.

## Kỹ thuật và an toàn
- Monolithic trong một backend, Controllers/Services/Features/Data tùy cấu trúc thực tế; DI, DTO/validation, HTTP status hợp lý, pagination/search khi cần, kiểm tra role/ownership ở backend.
- Chỉ thêm migration/schema MySQL nếu thực sự thiếu; ưu tiên thay đổi **additive**, xuất mô tả thay đổi; **không chạy migration phá dữ liệu**, không `DROP`, không seed đè dữ liệu thật. Hỏi trước nếu thao tác DB gây rủi ro.
- Không hardcode secret, không log key/connection string; không commit `.env` hay dữ liệu riêng tư.
- Không tự chuyển nhánh, merge, reset, force push hoặc push Git. **Chỉ hỏi khi định xóa file/logic, thay đổi dữ liệu nguy hiểm, hoặc gặp lựa chọn nghiệp vụ không thể suy ra an toàn**.

## KIỂM THỬ: tuyệt đối không tiêu tốn Gemini
- **KHÔNG gọi Gemini/Claude hay bất kỳ API AI, STT, TTS nào, kể cả health check, integration test, smoke test hoặc chạy demo AI.** Không gửi request thực ra Internet tới các dịch vụ này.
- Chỉ chạy build và test **cho chức năng mới/sửa lần này**: môn/lớp, ghi danh, upload, ngân hàng câu hỏi thủ công, kỳ thi/lịch thi, quyền truy cập, UI liên quan. Dùng mock/fake cho AI và ngoại vi.
- Không chạy toàn bộ test suite nếu trong đó có thể chứa test AI tốn phí; chọn test bằng filter/path. Với MySQL, chỉ đọc để xác minh kết nối; test ghi sử dụng database test tách biệt sau khi bảo đảm không đụng dữ liệu thật.
- Kiểm tra flow giảng viên → lớp → SV → tài liệu/câu hỏi → tạo lịch → SV xem lịch bằng test/mock/dữ liệu test phù hợp. Nêu rõ bước chưa thể kiểm thử thực tế.

## Tài liệu & báo cáo
- Tạo `docs/LECTURER_WORKFLOW.md`: sơ đồ/chuỗi bước nghiệp vụ ngắn, API chính, bảng/quan hệ bổ sung (nếu có), cách cấu hình MySQL an toàn, chạy frontend/backend, tài khoản/role test và kịch bản demo **không gọi AI**.
- Cuối cùng chỉ báo cáo NGẮN: file chính đã đổi, chức năng hoàn thành, kết quả build/test liên quan, DB có kết nối được không, việc còn thiếu. Không in nguyên file hoặc giải thích dài.

**Bắt đầu kiểm tra repository rồi triển khai đầy đủ LUỒNG GIẢNG VIÊN theo thứ tự trên. Ưu tiên tối thiểu thay đổi, chạy được, tiết kiệm token.**
