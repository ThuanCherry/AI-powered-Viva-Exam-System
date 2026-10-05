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
