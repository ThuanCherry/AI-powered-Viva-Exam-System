# AIVES — Codex Task Guide

## 1. Mục tiêu

Triển khai end-to-end cho project **AIVES – AI-powered Viva Exam System**:

1. **Login / Register / Logout**
2. **Role Authorization**
3. **Nhóm chức năng 2: Quản lý kỳ thi & lịch thi**
4. **Razor UI để demo**
5. **Development Sample Data / Seeder**
6. **Build + test + sửa lỗi đến khi chạy được**

Project hiện tại dùng:

- ASP.NET Core C#
- WebMVC / Razor Views
- Kiến trúc **3-Layers**
- MySQL
- Entity Framework Core
- Repository + UnitOfWork hiện có

---

# 2. Quyền làm việc của Codex

Codex được phép tự động:

- đọc toàn bộ source code;
- search/find file;
- đọc `AGENTS.md`, `SKILL.md`, `aives_db.txt`;
- tạo file mới;
- sửa code hiện có;
- refactor hợp lý;
- chạy `dotnet restore`;
- chạy `dotnet build`;
- chạy test;
- chạy project;
- đọc log;
- cài package nếu thật sự cần và tương thích;
- sửa lỗi compile/runtime trong phạm vi task.

## Chỉ hỏi trước khi

### 1. Xóa file hoặc folder

Khi cần xóa, hỏi ngắn:

```text
Cần xóa: <file/folder>

Lý do: <1-2 câu ngắn>

Tác động: <ảnh hưởng chính>

Cho phép xóa không?
```

### 2. Chạy lệnh destructive

Ví dụ:

```text
DROP DATABASE
DROP TABLE
reset schema
git reset --hard
force push
rebase có nguy cơ mất commit
```

Phải hỏi trước và giải thích ngắn.

## Git

Được phép:

```text
git status
git diff
git log
git branch
```

Không được tự:

```text
git push
git merge
git rebase
git reset --hard
```

trừ khi user yêu cầu.

---

# 3. Việc bắt buộc trước khi code

Trước khi sửa code, làm theo thứ tự:

1. Đọc `AGENTS.md` nếu có.
2. Đọc các `SKILL.md` liên quan nếu có.
3. Tìm và đọc `aives_db.txt`.
4. Xem `aives_db.txt` là **source of truth của database**.
5. Đọc solution hiện tại:
   - `AIVES.PresentationLayer`
   - `AIVES.BusinessLogicLayer`
   - `AIVES.DataAccessLayer`
6. Đọc:
   - `AivesDbContext`
   - Models / Entities
   - Repositories
   - `GenericRepository`
   - `UnitOfWork`
   - Interfaces
   - Services
   - Controllers
   - ViewModels
   - Razor Views
   - `ServiceCollectionExtensions`
   - `Program.cs`
7. Chạy:

```bash
dotnet build
```

8. Trước khi code chỉ báo ngắn:
   - bảng/FK liên quan;
   - class hiện có sẽ tái sử dụng;
   - file dự kiến tạo;
   - file dự kiến sửa.

Không giải thích dài.

---

# 4. Kiến trúc bắt buộc

Giữ đúng kiến trúc hiện tại:

```text
Presentation Layer (WebMVC)
Controllers
Razor Views
ViewModels
        ↓
Business Layer
Services
Interfaces
        ↓
Data Access Layer
Repositories
AivesDbContext
Entity Models
        ↓
MySQL
```

## Quy tắc

- Controller **không query DbContext trực tiếp**.
- Business Rule nằm trong Service.
- Repository chỉ xử lý truy cập dữ liệu/query.
- Entity/DbContext phải map theo `aives_db.txt`.
- Tái sử dụng code hiện có.
- Không tạo service/repository duplicate nếu không cần.

Ví dụ không nên tạo:

```text
ExamService2
NewExamService
TempExamRepository
NewUserService
```

nếu class hiện tại đã đúng trách nhiệm.

---

# 5. Scope bắt buộc

## 5.1 Login / Register

Làm đầy đủ:

- Login
- Register
- Logout
- Role Authorization

### Login

Yêu cầu:

- đăng nhập bằng account trong database;
- kiểm tra user hợp lệ;
- kiểm tra password hash;
- load Role từ schema thật;
- tạo authentication session;
- dùng claims phù hợp;
- Lecturer login → Exam Management;
- Student login → My Schedule.

Nếu project chưa có authentication mechanism phù hợp WebMVC:

- ưu tiên ASP.NET Core Cookie Authentication.

Claims tối thiểu:

```text
UserId
Email
Name
Role
```

### Register

Public Register:

- mặc định tạo role `STUDENT`;
- không cho user tự chọn `ADMIN`;
- không cho user tự chọn `LECTURER`;
- email unique;
- password phải hash;
- không lưu password plain text.

Form tối thiểu:

```text
Full Name
Email
Student Code (nếu DB yêu cầu)
Password
Confirm Password
```

Password tối thiểu:

- 8 ký tự;
- có chữ;
- có số.

---

# 6. Nhóm chức năng 2 — Quản lý kỳ thi & lịch thi

Requirement:

> Giảng viên tạo phiên thi vấn đáp, gắn môn học, danh sách sinh viên, khung thời gian mỗi thí sinh.  
> Hệ thống chọn ngẫu nhiên/thích ứng bộ câu hỏi cho từng sinh viên để tránh trùng lặp giữa các thí sinh thi liên tiếp.  
> Cho phép đặt số câu hỏi chính và số câu hỏi đào sâu tối đa mỗi thí sinh.

## Lecturer cần làm được

1. Xem danh sách Exam.
2. Tạo Exam.
3. Chọn Course.
4. Chọn Student thuộc Course.
5. Thiết lập:
   - Start time;
   - End time;
   - Duration per student;
   - Main Question Count;
   - Max Follow-up Questions;
   - Selection Strategy.
6. Generate Schedule.
7. Chỉnh time slot thủ công nếu cần.
8. Chọn Question Pool.
9. Generate Question Assignments.
10. Preview Assignments.
11. Publish Exam.

## Student cần làm được

- Login.
- Xem lịch thi của chính mình.
- Không xem lịch của Student khác.
- Không xem câu hỏi trước kỳ thi mặc định.

---

# 7. Business Rules

## Authentication

### AUTH-01
Email/account phải unique theo schema.

### AUTH-02
Public Register mặc định tạo `STUDENT`.

### AUTH-03
Password phải lưu dạng hash.

### AUTH-04
Role Authorization phải enforce ở backend.

---

## Exam

### BR-01
Lecturer chỉ tạo/sửa Exam thuộc Course mình phụ trách.

### BR-02

```text
starts_at < ends_at
```

### BR-03

```text
duration_minutes_per_student > 0
```

### BR-04

```text
main_question_count > 0
```

### BR-05

```text
max_follow_up_per_question >= 0
```

### BR-06
Student thêm vào Exam phải thuộc Course.

### BR-07
Một Student chỉ xuất hiện một lần trong một Exam.

### BR-08
Student time slot phải nằm trong thời gian Exam.

### BR-09
Nếu kỳ thi chạy tuần tự, các slot không được overlap.

### BR-10
Question Pool chỉ chứa Question đúng Course.

### BR-11
Chỉ dùng Question hợp lệ theo status/schema hiện tại.

Ví dụ nếu DB có:

```text
is_active
is_approved
status
```

thì phải lọc phù hợp.

### BR-12
Question Pool phải đủ để cấp:

```text
main_question_count
```

### BR-13
Một Student không nhận cùng một Question hai lần.

### BR-14
Ưu tiên tránh Question đã dùng cho N Student ngay trước đó.

### BR-15
Nếu Question Pool nhỏ:

- được relax recent-duplicate rule;
- nhưng vẫn không duplicate trong cùng một Student.

### BR-16
Publish chỉ khi:

```text
participants valid
schedule valid
question pool valid
assignments valid
```

### BR-17
Student chỉ xem schedule của chính mình.

### BR-18
Exam Published / Completed / Cancelled không được regenerate assignment âm thầm.

---

# 8. Generate Schedule

Logic tối thiểu:

```text
cursor = exam.starts_at

for student in orderedStudents:

    start = cursor
    end = start + duration_minutes_per_student

    if end > exam.ends_at:
        return validation error

    save:
        scheduled_start_at = start
        scheduled_end_at = end
        slot_order = incremental

    cursor = end
```

Nếu DB hiện tại không có `slot_order`:

- không tự thêm column;
- dùng `scheduled_start_at` để order;
- hoặc dùng model hiện tại.

---

# 9. RANDOM Question Assignment

Pseudo logic:

```text
participants = ordered by schedule

for participant in participants:

    candidates = valid questions from exam question pool

    recentQuestions =
        questions used by previous N participants

    preferred =
        candidates - recentQuestions

    if preferred đủ main_question_count:
        random từ preferred
    else:
        lấy preferred trước
        rồi fill từ candidates còn lại

    đảm bảo:
        không duplicate trong cùng participant

    save assignment + sequence
```

---

# 10. ADAPTIVE trong Function 2

Adaptive ở đây là:

> **pre-selection trước khi thi**

Không phải:

> AI follow-up real-time theo câu trả lời.

Nếu DB có metadata như:

```text
difficulty
bloom_level
topic
```

thì Adaptive cố gắng tạo bộ câu hỏi cân bằng.

Ví dụ:

```text
1 easy / remember
1 medium / understand
1 harder / apply-analyze
```

Tùy schema thực tế.

Nếu DB không đủ metadata:

- không tự thêm column;
- không đổi schema;
- fallback RANDOM;
- code structure vẫn nên cho phép Strategy abstraction.

Gợi ý:

```text
IQuestionSelectionStrategy

RandomQuestionSelectionStrategy

AdaptiveQuestionSelectionStrategy
```

và:

```text
QuestionAssignmentService
```

chọn strategy từ Exam config.

---

# 11. Cách triển khai theo Layer

## Presentation Layer

Ưu tiên tạo/mở rộng:

```text
AccountController
ExamsController
StudentController
```

ViewModels:

```text
LoginViewModel
RegisterViewModel
ExamCreateViewModel
ExamEditViewModel
ExamDetailsViewModel
ScheduleViewModel
QuestionPoolViewModel
AssignmentPreviewViewModel
StudentScheduleViewModel
```

Razor Views:

```text
Views/Account/Login.cshtml
Views/Account/Register.cshtml

Views/Exams/Index.cshtml
Views/Exams/Create.cshtml
Views/Exams/Edit.cshtml
Views/Exams/Details.cshtml

Views/Student/MySchedule.cshtml
```

Chỉ tạo nếu project chưa có tương đương.

---

## Business Layer

Tái sử dụng nếu đã có:

```text
IUserService
UserService

IExamService
ExamService

IExamSessionService
ExamSessionService
```

Nếu thiếu mới thêm service phù hợp.

Ví dụ:

```text
IQuestionAssignmentService
QuestionAssignmentService
```

hoặc:

```text
ISchedulingService
SchedulingService
```

chỉ khi logic hiện tại không phù hợp `ExamSessionService`.

---

## Data Access Layer

Dùng:

```text
AivesDbContext
GenericRepository
UnitOfWork
```

Repository đặc thù chỉ tạo khi query phức tạp.

Không query DB trực tiếp trong Controller.

---

# 12. Razor UI để demo

UI không cần quá đẹp.

Mục tiêu:

- dễ nhìn;
- dễ demo;
- validation rõ;
- flow rõ.

Dùng Razor + Bootstrap/CSS hiện tại.

Không thêm:

```text
React
Vue
Angular
```

---

# 13. Navigation

## Anonymous

```text
AIVES
Login
Register
```

## Lecturer

```text
Dashboard
Exams
Logout
```

## Student

```text
My Schedule
Logout
```

---

# 14. Login UI

Fields:

```text
Email
Password
Remember Me (optional)
```

Actions:

```text
Login
Register link
```

Có:

```text
Validation Summary
```

---

# 15. Register UI

Fields:

```text
Full Name
Email
Student Code (nếu schema cần)
Password
Confirm Password
```

Không hiển thị Role dropdown.

---

# 16. Exam List

Hiển thị:

```text
Title
Course
Start
End
Duration
Main Questions
Max Follow-up
Strategy
Status
Actions
```

Actions:

```text
Details
Edit
```

Edit chỉ khi business status cho phép.

---

# 17. Create / Edit Exam

Fields:

```text
Course
Title
Description
Start
End
Duration Minutes Per Student
Main Question Count
Max Follow-up
Selection Strategy
```

Nếu schema có:

```text
avoid_recent_duplicate_count
```

thì show/config.

Nếu DB không có:

- không thêm column chỉ vì UI cần;
- dùng config/service default nếu phù hợp.

---

# 18. Exam Details UI

Bố cục nên theo step:

```text
[Exam Header]

Step 1
Participants

Step 2
Schedule

Step 3
Question Pool

Step 4
Assignment Preview

Step 5
Publish
```

## Participants

- chọn Student thuộc Course;
- add selected;
- remove khi DRAFT.

## Schedule

Button:

```text
Generate Schedule
```

Table:

```text
Student
Start
End
Order
```

## Question Pool

List approved/active Question của Course.

Cho Lecturer chọn Question dùng cho Exam.

## Assignment Preview

Ví dụ:

```text
Student 01
Q1
Q4
Q9

Student 02
Q2
Q5
Q8

Student 03
Q1
Q6
Q10
```

UI nên giúp nhìn thấy câu trùng giữa slot gần nhau.

## Publish

Trước Publish validate toàn bộ.

---

# 19. Student — My Schedule

Student chỉ xem:

```text
Exam
Course
Start
End
Duration
Status
```

Không nhận `studentId` từ URL để query tùy ý nếu có thể lấy từ claims.

Dùng:

```text
CurrentUserId
```

từ authentication context.

---

# 20. Development Sample Data

Tạo:

```text
DevelopmentDataSeeder
```

Yêu cầu:

- chỉ chạy Development;
- idempotent;
- chạy nhiều lần không duplicate;
- không xóa data hiện có;
- password phải hash.

---

# 21. Demo Accounts

## Lecturer 1

```text
Email: lecturer1@aives.local
Password: Demo@123
```

## Lecturer 2

```text
Email: lecturer2@aives.local
Password: Demo@123
```

## Students

```text
student01@aives.local
student02@aives.local
student03@aives.local
student04@aives.local
student05@aives.local
student06@aives.local
student07@aives.local
student08@aives.local

Password: Demo@123
```

Password phải được hash.

---

# 22. Course Sample Data

## PRN222

```text
Advanced Programming with .NET
Lecturer: lecturer1
Students: student01 -> student08
```

## SWP391

```text
Software Development Project
Lecturer: lecturer2
Students: student01 -> student05
```

---

# 23. Question Sample Data

## PRN222

Tạo tối thiểu 12 câu.

Ví dụ:

1. Dependency Injection trong ASP.NET Core là gì?
2. Phân biệt Scoped, Transient và Singleton.
3. DbContext nên có lifetime thế nào?
4. Repository Pattern giải quyết vấn đề gì?
5. Unit of Work có vai trò gì?
6. Middleware Pipeline hoạt động thế nào?
7. Authentication khác Authorization thế nào?
8. async/await nên dùng khi nào?
9. LINQ deferred execution là gì?
10. EF Core Tracking và AsNoTracking khác nhau thế nào?
11. Làm sao hạn chế SQL Injection khi dùng EF Core?
12. Dependency Inversion trong SOLID là gì?

Nếu DB có:

```text
difficulty
bloom_level
topic
status
is_active
is_approved
```

thì seed hợp lý.

Không tự tạo field không tồn tại.

---

# 24. Demo Exam Data

## Exam 1

```text
PRN222 Viva - Draft Demo

Status:
DRAFT

Duration:
15 minutes/student

Main questions:
3

Max follow-up:
2

Strategy:
RANDOM
```

Dùng để demo:

```text
participants
generate schedule
question pool
generate assignments
publish
```

## Exam 2

```text
PRN222 Viva - Published Demo
```

Có sẵn:

```text
participants
schedule
assignments
```

để Student login xem `My Schedule`.

---

# 25. Authorization & Security

Phải có backend authorization.

Không chỉ ẩn menu.

## Lecturer

Có quyền:

```text
Exam Management
```

## Student

Có quyền:

```text
My Schedule
```

Student không truy cập:

```text
/Exams
```

Lecturer ownership/course assignment phải check trong Business Layer.

---

# 26. MVC Security

POST form:

```text
AntiForgeryToken
```

Validation:

```text
DataAnnotations
ModelState
Business validation
```

Không log:

```text
Password
Password Hash
Database Password
Connection String
```

Không trust:

```text
StudentId
LecturerId
```

từ hidden field nếu có thể lấy từ authenticated user.

---

# 27. Database Rules

Database hiện tại là MySQL.

Task này:

```text
MySQL ONLY
```

Không thêm:

```text
SQL Server
PostgreSQL
```

Không:

```text
DROP DB
DROP TABLE
reset schema
```

Không tạo Migration nếu database hiện tại đã đủ.

Nếu mapping C# lệch DB:

> sửa mapping/code theo `aives_db.txt`.

---

# 28. Implementation Phases

## Phase 0 — Discovery

- đọc docs/source;
- đọc `aives_db.txt`;
- build;
- xác định mapping.

## Phase 1 — Auth

- Register;
- Login;
- Logout;
- claims;
- role authorization.

## Phase 2 — Exam Core

- list;
- create;
- edit;
- details;
- ownership validation.

## Phase 3 — Participants & Schedule

- add/remove students;
- generate schedule;
- manual edit;
- overlap validation.

## Phase 4 — Question Pool & Assignment

- pool;
- RANDOM;
- ADAPTIVE/fallback;
- recent duplicate avoidance;
- preview.

## Phase 5 — Publish & Student View

- publish validation;
- Student My Schedule.

## Phase 6 — Seeder

- demo accounts;
- courses;
- students;
- questions;
- draft/published exams.

## Phase 7 — QA

- build;
- test flow;
- sửa lỗi;
- remove debug code.

---

# 29. Acceptance Checklist

Codex chỉ kết thúc task khi tối thiểu đạt:

```text
[ ] dotnet build thành công

[ ] Login hoạt động
[ ] Register hoạt động
[ ] Logout hoạt động
[ ] Role Authorization hoạt động

[ ] Lecturer xem Exam List
[ ] Lecturer Create Exam
[ ] Lecturer Edit Draft Exam
[ ] Lecturer Details Exam

[ ] Add Participants
[ ] Không duplicate Student
[ ] Generate Schedule
[ ] Không overlap
[ ] Không vượt End Time

[ ] Select Question Pool
[ ] Generate Assignments
[ ] Đủ main_question_count khi pool đủ
[ ] Không duplicate question trong cùng Student
[ ] Recent duplicate avoidance hoạt động

[ ] ADAPTIVE dùng metadata hiện có hoặc fallback rõ ràng

[ ] Publish validation hoạt động

[ ] Student My Schedule hoạt động
[ ] Student không xem schedule của người khác

[ ] Seeder idempotent
[ ] Demo accounts login được

[ ] Không log secret
[ ] Không thêm SQL Server/PostgreSQL
```

---

# 30. Flow Demo trước giảng viên

## Bước 1 — Register

Tạo Student mới.

Chứng minh:

```text
Role = STUDENT
```

---

## Bước 2 — Login Lecturer

```text
lecturer1@aives.local
Demo@123
```

---

## Bước 3 — Create Exam

Chọn:

```text
Course: PRN222

Duration:
15 minutes

Main Questions:
3

Max Follow-up:
2

Strategy:
RANDOM
```

---

## Bước 4 — Participants

Chọn 5 Student.

---

## Bước 5 — Generate Schedule

Chứng minh:

```text
Student A 08:00 - 08:15
Student B 08:15 - 08:30
Student C 08:30 - 08:45
...
```

Không overlap.

---

## Bước 6 — Question Pool

Chọn khoảng:

```text
10-12 questions
```

---

## Bước 7 — Generate Assignments

Chứng minh:

```text
mỗi Student = 3 questions
```

và hạn chế trùng giữa Student liền nhau.

---

## Bước 8 — Publish

Validate rồi Publish.

---

## Bước 9 — Student Login

```text
student01@aives.local
Demo@123
```

Mở:

```text
My Schedule
```

Chứng minh Student chỉ thấy lịch của mình.

---

# 31. Output cuối cùng Codex phải báo

Không giải thích dài.

Chỉ báo:

## Files

```text
Created:
...

Modified:
...
```

## Demo accounts

```text
Lecturer:
...

Student:
...
```

## Run

```bash
dotnet run --project ...
```

## Demo Flow

```text
Login Lecturer
-> Create Exam
-> Participants
-> Schedule
-> Pool
-> Assignments
-> Publish
-> Login Student
-> My Schedule
```

## Remaining Issues

Chỉ liệt kê nếu còn.

---

# 32. Prompt chính cho Codex

Copy/paste đoạn dưới đây nếu cần:

```text
Đọc toàn bộ file docs/AIVES_Function2_Codex_Guide.md này và làm theo.

Bạn có toàn quyền đọc source, search file, tạo file, sửa code,
restore, build, test, run và sửa lỗi cho đến khi feature chạy end-to-end.

Không cần hỏi trước khi:
- đọc;
- tạo file;
- sửa code;
- build;
- test;
- run;
- cài package cần thiết.

CHỈ hỏi trước khi:
1. xóa file/folder;
2. chạy destructive command như DROP DB/reset schema/git reset --hard.

Khi hỏi xóa chỉ giải thích ngắn:
- xóa gì;
- tại sao;
- tác động.

Trước khi code:
- đọc AGENTS.md/SKILL.md nếu có;
- đọc docs/aives_db.txt;
- đọc 3-Layers source hiện tại;
- đọc DbContext/Models/Repositories/UnitOfWork/Services/Controllers/Views;
- chạy dotnet build;
- báo ngắn file sẽ tái sử dụng + file dự kiến tạo/sửa.

Sau đó tự triển khai:
A. Login/Register/Logout + Role Authorization;
B. Function 2 Exam & Scheduling;
C. RANDOM/ADAPTIVE pre-selection + tránh trùng câu gần nhau;
D. Razor UI để demo;
E. Development Seeder idempotent;
F. build/test và sửa hết lỗi.

Giữ đúng:
Presentation -> Business -> DataAccess -> MySQL.

Không query DbContext trực tiếp từ Controller.
Không tự đổi schema nếu aives_db.txt đã đủ.
Không thêm SQL Server/PostgreSQL.
Không push GitHub.

Không dừng ở skeleton.
Hoàn thiện flow chạy được end-to-end.

Khi xong chỉ báo ngắn:
- file đã tạo/sửa;
- account demo;
- flow demo;
- lệnh chạy;
- issue còn lại.
```
