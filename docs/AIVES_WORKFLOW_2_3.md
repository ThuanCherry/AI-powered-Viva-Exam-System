# Workflow 2 & 3 — AIVES

> **Cập nhật Speech:** nghiệp vụ MySQL/lịch/deadline/follow-up bên dưới giữ nguyên. STT hiện dùng Gemini Live Transcribe (PCM16/16kHz, frame 100ms, interim/final qua SignalR), TTS dùng Gemini Interactions và trả WAV. Các mô tả Azure/MediaRecorder 8 giây trong ghi chép cũ đã được thay ở runtime; xem [GEMINI_SETUP.md](GEMINI_SETUP.md) cho kiến trúc/cấu hình/test hiện tại.

## Kiến trúc và audit

React JSX tại root → REST + SignalR → **một** ASP.NET Core monolith `backend/AIVES.Api` → MySQL 8 hiện có. .NET 10, Pomelo 9.0.0, EF Core 9.0.20 được dùng thống nhất; không thêm backend 3-layer hay database khác cho ứng dụng. SQLite chỉ phục vụ unit/integration tests trong bộ nhớ.

Audit thực: nhánh `feature/swd-monolith`; connection ban đầu trỏ `defaultdb` không tồn tại (1049). Đã truy vấn MySQL thực, tìm `aives_db` và đối chiếu cột/enum với dump. `aives_swd` dùng schema khác, không được chọn. `aives_db` ban đầu không có user/câu hỏi; công cụ smoke thêm fixture demo riêng. **Không import `backend/database/MySQL_AIVES.sql`**: dump có DROP TABLE. Không chạy migration, không thay schema, không xóa dữ liệu.

Cấu hình local riêng `backend/AIVES.Api/appsettings.Development.local.json` đang chọn `aives_db`; file được gitignore. Giữ nguyên thay đổi có trước của người dùng trong `appsettings.json`.

## Actor và quyền

- Lecturer: chỉ quản lý kỳ thi/môn học do mình phụ trách; chọn Student đang Active, thuộc academic_classes đang active của môn qua class_students.
- Student: chỉ xem lịch đã công bố và phiên của bản thân, không xem danh sách người khác. Không truy cập ngân hàng có expectedAnswer/đáp án đúng.
- Quyền được kiểm tra ở API; role guard React chỉ điều hướng. JWT lấy từ login, gửi `Authorization: Bearer ...`; key vendor không đi xuống React.

## Nhóm 2: cấu hình → lịch nháp → công bố

1. Vào `/exams`, chọn **Tạo kỳ thi**, chọn môn học thực từ catalog.
2. Chọn thời gian Việt Nam, số phút mỗi sinh viên, câu chính, follow-up tổng mỗi attempt, follow-up mỗi câu chính, giây mỗi lượt và chiến lược.
3. Chọn sinh viên: thứ tự chọn là thứ tự slot. Bỏ chọn rồi chọn lại để chuyển xuống cuối. Preview cập nhật ngay, chưa ghi DB.
4. **Lưu bản nháp & xem trước**: backend tự tính end = start + số SV × phút/SV, xếp slot tuần tự; kiểm tra quyền, enrollment, pool Approved/Main, lịch giảng viên/lớp và slot sinh viên. Frontend chỉ chọn giờ bắt đầu, hiển thị kết thúc chỉ đọc; chỉ sửa nháp chưa bắt đầu.
5. Sửa nháp được; sinh viên bỏ chọn được đánh dấu Cancelled trong bảng cũ, không xóa. Lịch nháp cũng giữ chỗ để phát hiện trùng lịch. Các lịch Completed/Cancelled không giữ chỗ nữa.
6. **Công bố bản nháp đã lưu** kiểm tra lại lịch/pool và phân câu trong cùng transaction. Không công bố khi form còn sửa chưa lưu. `Scheduled` là tên trạng thái công bố theo enum MySQL, không dùng `Published` ngoài schema.

States: `Draft → Scheduled → InProgress → Completed`; `Draft/Scheduled → Cancelled`. Chỉ sửa Draft; không hủy phiên đã bắt đầu. Công bố lại Scheduled trả kết quả idempotent.

`time_per_student` dùng **phút**, `seconds_per_question` dùng giây. Request timestamp có offset rõ ràng, ví dụ `2026-10-19T09:00:00+07:00`; backend lưu UTC trong DATETIME và trả timestamp UTC có `Z` ở API chi tiết/lịch/attempt. React luôn hiển thị `Asia/Ho_Chi_Minh`. List Exams cũ vẫn trả DATETIME không suffix vì giữ hợp đồng cũ; helper UI xem giá trị này là UTC. Không tự chuyển giá trị lịch legacy: nếu DB có dữ liệu từng lưu theo giờ local, cần xác nhận quy ước trước khi sử dụng các lịch đó với engine mới.

### Phân câu

- Chỉ lấy câu Main, Approved của môn. Không tạo lại question bank, không sửa câu hỏi/rubric.
- Random ưu tiên câu không nằm trong **hai thí sinh gần nhất**, sau đó ít dùng trong tối đa 1.000 assignment gần nhất của môn, rồi random để phá hòa.
- Adaptive hiện tại là **phân phối cân bằng độ khó** Easy → Medium → Hard với ưu tiên chống trùng. Không gọi đây là thích nghi theo năng lực hay đổi câu tiếp theo dựa trên điểm. Tất cả câu chính được phân khi công bố để bảo đảm refresh không đổi đề.
- Pool < số câu chính: lỗi 400. Pool < 3 × số câu chính: công bố được nhưng trả cảnh báo có thể trùng giữa các sinh viên gần nhau. Không vòng lặp chọn vô hạn.
- Assignment lưu `exam_questions`, một lượt chính mỗi câu mỗi candidate. Hai thao tác công bố được serialize bằng khóa InnoDB trên các role rows hiện có, rồi kiểm tra trạng thái trong transaction. Unique `(exam_id, student_id)` và `answers.exam_question_id` của schema được tái sử dụng. Không thêm constraint chưa được duyệt. Không có đường ghi assignment khác trong workflow này.
- Khóa role rows dùng chung để ngăn hai giảng viên/môn khác nhau cùng xếp trùng một sinh viên. Đây là đánh đổi đơn giản cho quy mô đồ án; không giữ khóa lịch trong khi gọi AI.

## Nhóm 3: vào phòng → vấn đáp → hoàn tất

1. `/exams` với Student hiển thị `schedule/mine`; vào `/viva/:examId` là phòng chờ. Câu chính chưa hỏi không được trả xuống client.
2. Bắt đầu chỉ khi thuộc danh sách, kỳ thi Scheduled/InProgress, server time nằm trong slot và khung giờ chung. Một hàng candidate tương ứng một attempt. Bắt đầu lặp hoặc đồng thời trả cùng phiên.
3. Deadline phiên = min(end_time, scheduled_time + slot, started_at + slot). Đến muộn chỉ được phần slot còn lại. Mỗi lượt có deadline riêng từ asked_at + seconds_per_question, chặn bởi deadline phiên.
4. Backend TTS đọc **câu hiện tại**; client không được gửi văn bản tùy ý để đọc. Lỗi autoplay có nút Nghe câu hỏi để thử lại. Khi chưa có Azure, nút bị vô hiệu và trạng thái NotConfigured hiện rõ.
5. `MediaRecorder.isTypeSupported` chọn webm/opus, ogg/opus hoặc mp4. Mỗi đoạn ~8 giây là một bản ghi hoàn chỉnh; Web Audio decode/resample thành PCM WAV 16kHz mono 16bit trước khi upload. Azure REST short audio trả transcript sau mỗi đoạn; **không phải streaming tức thời**. Dừng/khởi tạo recorder có thể có khe hở nhỏ ở biên đoạn; cần kiểm thử mic thực trên Brave/Chrome với provider.
6. STT giới hạn canonical WAV ≤15 giây / 480044 bytes, request ≤500000 bytes, kiểm tra RIFF/fmt/data/sample rate/codec. Backend chỉ nhận cho lượt đang mở và còn giờ sau khi provider trả về. Chunk UUID chống gửi trùng, transcript lưu vào Answer/FollowUpQuestion và log AudioChunk. Không lưu raw audio trong triển khai này; transcript/timestamps/lỗi được lưu bền vững. Hàng đợi client có backpressure và timeout 35 giây; provider timeout 25 giây, retry một lần khi 429/5xx.
7. Student có thể sửa transcript hoặc **nhập văn bản dự phòng**. Phần gõ tay chưa nộp nằm trong bộ nhớ trang; refresh sẽ mất phần chưa nộp. Transcript STT đã nhận và câu trả lời đã nộp được khôi phục từ MySQL. Không tự gọi browser SpeechRecognition.
8. Nộp với `turnId` do server cấp (`q-<id>` hoặc `f-<id>`). Backend lưu lời đáp, đưa câu chính + expectedAnswer + rubric có sẵn + câu hiện tại + lịch sử cùng câu chính vào Gemini. Không gửi toàn bộ ngân hàng. Gemini trả quyết định có/không cần hỏi thêm và một câu gắn trực tiếp ý thiếu/mơ hồ/mâu thuẫn.
9. Backend luôn áp giới hạn tổng follow-up/attempt và follow-up/câu chính, kiểm tra deadline lại sau khi LLM trả, bỏ câu trùng câu chính/follow-up trước. Provider không có quyền tăng ngân sách. Không mặc định mỗi câu đều có follow-up.
10. Hết lượt follow-up, trả lời đủ ý, bỏ qua hoặc timeout → câu chính tiếp theo. Hết câu chính/deadline → Completed. Nếu LLM lỗi/NotConfigured, lời đáp vẫn lưu, lỗi ghi exam_logs và chuyển tiếp không tạo follow-up giả. Không có chấm điểm cuối tự động trong phạm vi workflow này.

Main: `exam_questions` + `answers`; follow-up: bảng **follow_up_questions hiện có**, liên kết Answer của câu chính. Không tạo thêm Question trong bank cho follow-up AI. Cột parent_exam_question_id legacy được giữ nguyên. Log provider chỉ chứa thông báo an toàn/HTTP code, không key, không response body vendor. `reason` không trả cho sinh viên.

Khóa hàng exam trong transaction serialize start/answer/STT/timeout, kể cả nhiều backend process dùng cùng MySQL. Gọi provider giữ khóa exam tối đa timeout nên các sinh viên cùng exam có thể phải chờ; phù hợp lịch thi tuần tự hiện tại. Không bật execution retry DB để tránh nhân đôi side effect. Retry request với turnId đã hoàn tất trả trạng thái, không sửa transcript cũ. Client dùng polling REST mỗi 5 giây để đồng bộ bền vững và SignalR cho notification.

Hosted worker 5 giây quét Scheduled/InProgress kể cả khi client đóng: timeout lượt/phiên; candidate Waiting hết slot thành Absent; exam hoàn thành khi không còn Waiting/InProgress. Worker cần backend còn chạy; nếu backend ngừng, request/worker đầu tiên sau restart tính lại từ timestamp đã lưu. Request resume cũng áp deadline ngay, UI countdown không có quyền quyết định giờ.

SignalR `/hubs/exam` yêu cầu JWT. `JoinExam(examId)` kiểm tra membership/ownership; nhóm riêng `student-{userId}-exam-{examId}`. Events `ServerTime`, `TranscriptUpdated`, `AttemptUpdated` chỉ phát từ backend. Join lại khi reconnect; nếu hub lỗi vẫn dùng REST, UI thông báo trạng thái. Không broadcast transcript cho toàn bộ kỳ thi. API SendMessage legacy chặn phát event tùy ý.

## Hợp đồng API

JSON thành công `{ "success": true, "data": ... }`; lỗi nghiệp vụ `{ "success": false, "message": "..." }`. 400 dữ liệu sai, 401 chưa login, 403 thiếu quyền, 404 không tồn tại, 409 xung đột/trạng thái/giờ, 415 audio sai, 422 SpeechNoMatch, 502 provider lỗi, 503 NotConfigured, 504 provider timeout.

| Method/route | Quyền | Mục đích |
|---|---|---|
| POST `/api/Users/login` | Public | `{email,password}` → userId,fullName,email,role,token |
| GET `/health` | Public | Kết nối DB thực; 503 nếu unavailable |
| GET `/api/workflow/providers` | Login | Trạng thái cấu hình; không lộ key, Configured chưa có nghĩa probe thành công |
| GET `/api/workflow/catalog` | Lecturer | Môn mình quản lý + poolSize |
| GET `/api/courses/{id}/students` | Lecturer chủ môn | Student Active thuộc lớp môn |
| GET `/api/Exams` | Lecturer/Student | Lecturer: kỳ thi mình; Student: được công bố, có membership |
| GET `/api/Exams/{id}` | Chủ kỳ thi/Student được gán | Chi tiết; Student chỉ thấy candidate của mình |
| POST `/api/Exams` | Lecturer | Tạo nháp |
| PUT `/api/Exams/{id}` | Chủ kỳ thi | Sửa nháp, xếp lại slot |
| POST `/api/Exams/{id}/publish` | Chủ kỳ thi | Validate lại, persist assignment, Scheduled, warnings[] |
| POST `/api/Exams/{id}/cancel` | Chủ kỳ thi | Hủy Draft/Scheduled, giữ các rows |
| GET `/api/schedule/mine` | Student | Lịch cá nhân |
| POST `/api/Exams/{id}/attempt/start` | Student được gán | Bắt đầu/resume idempotent |
| GET `/api/Exams/{id}/attempt` | Student được gán | Resume + đồng bộ timeout |
| POST `/api/Exams/{id}/attempt/answer` | Student được gán | Nộp `{turnId,transcript}`; transcript ≤12000 ký tự |
| POST `/api/Exams/{id}/attempt/transcribe` | Student được gán | multipart `turnId`, `chunkId` UUID, `audio` WAV |
| GET `/api/Exams/{id}/attempt/tts?turnId=q-123` | Student được gán | MP3 câu đang hỏi, không text tùy ý |

Ví dụ tạo kỳ thi (đổi start theo ngày demo, end được backend tính tự động):

```json
{
  "courseId": 1, "classId": 1, "examName": "Vấn đáp SWD", "description": "Cuối kỳ",
  "startTime": "2026-10-19T09:00:00+07:00",
  "timePerStudent": 15, "mainQuestionCount": 3, "maxFollowUpCount": 2,
  "maxFollowUpsPerQuestion": 1, "secondsPerQuestion": 120,
  "strategy": "Random", "studentIds": [2, 3]
}
```

Response `{ "success": true, "data": { "examId": 54 } }`. Dùng ID thật từ catalog/students, không mặc định tồn tại ID trong ví dụ.

Ví dụ state rút gọn:

```json
{
  "success": true,
  "data": {
    "examId": 54, "candidateId": 90, "status": "InProgress",
    "serverTime": "2026-10-19T02:00:05.0000000Z",
    "deadline": "2026-10-19T02:15:00.0000000Z", "turnDeadline": "2026-10-19T02:02:00.0000000Z",
    "turn": { "turnId": "q-123", "text": "Transaction là gì?", "mainOrder": 1, "isFollowUp": false, "transcript": "" },
    "followUpsUsed": 0, "maxFollowUpCount": 2
  }
}
```

Frontend routes cũ `/exams/:examId/config` vẫn tồn tại, dẫn vào editor nối API thật. Dashboard dẫn danh sách kỳ thi thực. Các file mock/legacy giữ lại theo quy tắc không xóa; không nằm trên hai workflow mới. Các mục sidebar chưa có triển khai được ẩn.
