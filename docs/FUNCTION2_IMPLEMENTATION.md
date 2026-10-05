# Function 2 Implementation

## Files

Reused and updated `AivesDbContext`, `GenericRepository`, `UnitOfWork`, `UserService`, `CourseService`, `ExamService`, `ExamSessionService` and existing controllers/view models.

Added:
- `Models/Function2Entities.cs`: roles, membership, participants, pool and assignments.
- `Common/Function2Contracts.cs`, `ExamRules`, `QuestionAssignmentService`, RANDOM/ADAPTIVE strategies and `DevelopmentDataSeeder`.
- `AccountController`, `StudentController`, account/exam/student Razor views and `function2.css`.
- `tests/AIVES.Tests` and `tests/e2e_function2.py`.
- Course management contracts/view models, `Views/Courses`, automatic scheduling tests and `wwwroot/js/exam-planning.js`.

## Database and compatibility

The current app maps `aives_swd` exactly as defined by `docs/AIVES_DB.txt`. IDs are signed BIGINT/long, and roles are read through `user_roles`. No tables or columns were changed. Unused legacy `ExamSession.cs` and `StudentAnswer.cs` files are preserved and explicitly excluded from the EF model because the authoritative schema does not contain their tables.

Legacy unsecured endpoints were replaced with authenticated, scoped endpoints:
- GET `/api/exams`: current lecturer's owned exams.
- GET `/api/courses`: current lecturer's assigned courses.
- GET `/api/users/me`: current identity, without passwords/hashes.
- GET `/api/examsessions/mine`: current student's published schedule.

Login/Register/Logout use MVC forms and cookie authentication. POSTs require antiforgery tokens. No public role selector or caller-supplied owner/student ID controls access.

## Validation and transactions

Business services enforce configuration, lecturer ownership, active student enrollment, correct approved question pool, schedule bounds/overlap, unique assignments and publish readiness. Every exam mutation takes a MySQL row lock in a transaction; validation failures roll back. Schedule or pool changes clear stale DRAFT assignments and require explicit regeneration. Publication prevents further editing/regeneration.

Course management reuses `CourseService` and `CoursesController`. Lecturers manage assigned courses, enroll active STUDENT accounts and add/edit course questions. New courses assign their creator automatically. Course row locks coordinate question edits with exam publication; transactions use READ COMMITTED so validations see committed changes after locking. Published question content cannot be silently changed.

Scheduling now takes only start time and minutes per student. Saving participants, editing exam configuration, or saving the schedule recalculates adjacent appointments and the exam end. Users do not enter each student's end time. Partial question-pool selections are saved with a specific shortage message; generating assignments and publishing still require enough valid questions.

RANDOM prioritizes candidates absent from the previous N students. ADAPTIVE uses the same recent-avoidance priority while balancing Easy/Remember, Medium/Understand and Hard/Apply-style metadata. With a small pool, recent repetition is allowed but never duplication within one participant. Preview identifies recent repeats.

## Demo and checks

Accounts: lecturer1/lecturer2 and student01 through student08 at `@aives.local`; password `Demo@123`.

Flow: Login lecturer → Create/Edit DRAFT → Participants → Generate Schedule → Question Pool → Generate Assignments → Preview → Publish → Logout → Login student → My Schedule.

Validated:
- Build: zero warnings and errors.
- 26 xUnit checks for rules, strategies, schema mapping and automatic 15/20-minute scheduling.
- 72 HTTP/MySQL acceptance checks, including full RANDOM/ADAPTIVE flows, authorization, CSRF, registration, invalid slots/pools, course/question creation and editing, ownership, published-question protection, automatic scheduling, publication and student isolation.
- Server restart: counts unchanged across all 11 mapped tables; seeder is idempotent.

The acceptance test retains uniquely named QA records. The Development seeder never resets user edits. Real-time viva answer scoring and AI follow-up generation are outside Function 2; this implementation provides pre-selection and the configured maximum follow-up count.
