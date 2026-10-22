<<<<<<< HEAD
# AIVES — AI-powered Viva Exam System

ASP.NET Core MVC / Razor, .NET 10, EF Core, and MySQL. Function 2 manages viva exams, participants, schedules, question pools, pre-selection and publication.

## Architecture

![3-Layers architecture](docs/images/Kien%20truc%203-layers.png)

- `src/AIVES.PresentationLayer`: cookie authentication, role-protected controllers, forms and Razor views.
- `src/AIVES.BusinessLogicLayer`: authentication, ownership validation, scheduling, RANDOM/ADAPTIVE selection, publication and Development seeding.
- `src/AIVES.DataAccessLayer`: mapped entities, repositories, transactions and MySQL.
- `tests/AIVES.Tests`: xUnit rule/selection/mapping tests.
- `tests/e2e_function2.py`: real HTTP/MySQL acceptance flow.

Controllers call Business services. Database queries and per-exam transaction locks stay in DataAccess.

## Database configuration

**`docs/AIVES_DB.txt` is the schema source of truth.** Its dump identifies `aives_swd`; that existing database contains the complete Function 2 schema. Configure `ConnectionStrings:DefaultConnection` with your MySQL host, port, database, username and password.

Example (replace the password locally):

```text
Server=localhost;Port=3306;Database=aives_swd;User=root;Password=YOUR_PASSWORD;
```

You can override it with `ConnectionStrings__DefaultConnection` in the environment. Development does not replace this connection setting. DBeaver is used to inspect the same MySQL database.

The app never runs migrations, creates tables, or resets the database. **Do not rerun the SQL dump on an existing database:** it contains DROP statements. The old six-table `aives_db` database does not match this Function 2 schema.

## Run

Install the .NET 10 SDK and start MySQL, then:

```powershell
dotnet restore AIVES.slnx
dotnet build AIVES.slnx
dotnet run --project src/AIVES.PresentationLayer --launch-profile http
```

Open **http://localhost:5000** (also available at http://localhost:5234). Development automatically adds sample data without deleting existing rows. Date/time form values and schedules use local exam time; audit fields use UTC.

## Demo accounts

All demo accounts use **`Demo@123`**. Passwords are salted hashes.

| Role | Email | Assigned course |
| --- | --- | --- |
| Lecturer | lecturer1@aives.local | PRN222 |
| Lecturer | lecturer2@aives.local | SWP391 |
| Student | student01@aives.local through student08@aives.local | PRN222; first five also SWP391 |

The seeder adds 12 approved PRN222 questions, a DRAFT exam and a PUBLISHED exam with a ready schedule. It preserves existing demo exam edits on restart. Public registration always creates STUDENT, without automatic course enrollment.

## Demo flow

Lecturers can open **Môn học & câu hỏi** to add/edit a course, enroll active students, and add/edit its questions. New courses are assigned to the lecturer who creates them. Questions in published exams are protected; add a new question to preserve an existing published paper.

1. Login as lecturer1 and open **PRN222 Viva - Draft Demo**, or create an exam.
2. Select enrolled students and save; their appointments are generated automatically.
3. Set the first student's start time and minutes per student (e.g. 15 or 20). Save to recalculate all appointments and the exam end time automatically.
4. Select at least the configured number of approved questions in Question Pool.
5. Generate Assignments and inspect Preview; try RANDOM or ADAPTIVE.
6. Publish, logout, then login as student01 and open **My Schedule**.

DRAFT configuration/participant/schedule/pool changes invalidate existing previews explicitly. Published exams cannot be edited or regenerated. Small pools may reuse recent questions between students, with a visible warning; questions never duplicate within one student. ADAPTIVE balances difficulty/Bloom metadata and falls back to RANDOM if metadata is absent.

Question Pool means the questions available to this exam. If each student needs 3 main questions, select at least 3 approved/active questions. Partial selections can be saved, but assignment generation and publication explain exactly how many more questions are needed. The page provides a live selection count and a select-all button.

Student routes derive identity from claims and return only their schedule, never question assignments. Every modifying form checks antiforgery tokens; services enforce lecturer ownership/course assignment.

## Verification

```powershell
dotnet test AIVES.slnx
# With the Development server running:
python tests/e2e_function2.py
```

The Python acceptance test uses the connection in appsettings.json and a MySQL CLI installed at the standard Windows location. It adds uniquely named QA accounts/exams and preserves all existing records. See [implementation and verification notes](docs/FUNCTION2_IMPLEMENTATION.md).
=======
# AI-powered-Viva-Exam-System

React JSX + ASP.NET Core monolith + MySQL hiện có.

- [Kiến trúc hiện tại — draw.io chỉnh sửa được](docs/architecture/AIVES_ARCHITECTURE.drawio) · [Xem SVG](docs/architecture/AIVES_ARCHITECTURE.svg)
- [Kết quả tích hợp dev và kiểm thử workflow 2](docs/MERGE_DEV_WORKFLOW2.md)

- [Workflow giảng viên, API và demo cô lập không gọi AI](docs/LECTURER_WORKFLOW.md)
- [Chạy thật, tài khoản demo và kiểm thử](docs/RUN_AND_TEST.md)
- [Workflow lịch thi và AI vấn đáp, API, states](docs/AIVES_WORKFLOW_2_3.md)
- [Cấu hình keys Gemini/Azure ở backend](docs/AI_KEYS_AND_SETUP.md)
- [Gemini Live Transcribe + TTS + LLM: cấu hình và demo hiện tại](docs/GEMINI_SETUP.md)

Tại root repo, sau khi MySQL đang chạy và cấu hình connection/JWT:

Máy mới: nếu chưa có `backend/AIVES.Api/appsettings.json`, sao chép từ `appsettings.example.json` cùng thư mục và điền MySQL/JWT. Không ghi đè cấu hình local có sẵn. API key thật chỉ nằm trong file local bị Git bỏ qua hoặc User Secrets.

```powershell
npm ci
dotnet run --project backend/AIVES.Api
npm run dev
```

Hai lệnh cuối chạy trong hai terminal riêng. Mở http://localhost:5173.
Không tự import SQL dump: dump chứa DROP TABLE. Xem hướng dẫn chạy trước khi thao tác DB.

`src/` chỉ chứa frontend React; backend Monolithic nằm tại `backend/AIVES.Api`. Mở `AIVES.slnx` trong Visual Studio để làm việc với backend. Xem [kiến trúc runtime hiện tại](docs/architecture/README.md).
>>>>>>> 439d0a1b8da74ba41acba8d03bc971c895af2f4e
