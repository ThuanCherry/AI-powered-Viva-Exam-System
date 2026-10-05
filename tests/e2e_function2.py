"""Real MySQL + HTTP acceptance flow. Requires the Development server on localhost:5000.
Creates uniquely named QA users/exams; preserves all existing records and tables.
Run: python tests/e2e_function2.py
"""
import csv
import html
import io
import json
import os
from pathlib import Path
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import http.cookiejar
import uuid
from datetime import datetime, timedelta

BASE = os.environ.get("AIVES_TEST_URL", "http://localhost:5000")
RUN = uuid.uuid4().hex[:10]
CHECKS = []

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

class Session:
    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
    def request(self, path, data=None, redirect=True):
        opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookies),
            *([] if redirect else [NoRedirect()]))
        encoded = urllib.parse.urlencode(data, doseq=True).encode() if data is not None else None
        req = urllib.request.Request(BASE + path, data=encoded)
        try:
            response = opener.open(req, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        return response.code, html.unescape(response.read().decode("utf-8")), dict(response.headers)
    def token(self, path):
        status, body, _ = self.request(path)
        assert status == 200, (path, status)
        inputs = re.findall(r'<input\b[^>]*>', body)
        for tag in inputs:
            if 'name="__RequestVerificationToken"' in tag:
                return html.unescape(re.search(r'value="([^"]+)"', tag).group(1))
        raise AssertionError("Missing antiforgery token: " + path)
    def post(self, path, fields, token_path=None, redirect=True):
        values = list(fields.items()) if isinstance(fields, dict) else list(fields)
        values.append(("__RequestVerificationToken", self.token(token_path or path)))
        return self.request(path, values, redirect)
    def login(self, email, password="Demo@123"):
        status, _, headers = self.post("/Account/Login", {"Email": email, "Password": password}, redirect=False)
        assert status == 302, ("login", email, status)
        return headers.get("Location", "")
    def api(self, path):
        status, body, _ = self.request(path)
        assert status == 200, (path, status)
        return json.loads(body)

def check(condition, label):
    assert condition, label
    CHECKS.append(label)
    print("PASS:", label)

def sql(query):
    config = json.loads(Path("src/AIVES.PresentationLayer/appsettings.json").read_text(encoding="utf-8-sig"))
    parts = dict(item.split("=", 1) for item in config["ConnectionStrings"]["DefaultConnection"].split(";") if "=" in item)
    env = os.environ.copy()
    env["MYSQL_PWD"] = parts["Password"]
    result = subprocess.run([
        r"C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe",
        "--host=" + parts["Server"], "--port=" + parts.get("Port", "3306"),
        "--user=" + parts["User"], "--database=" + parts["Database"], "--batch",
        "--execute=" + query
    ], env=env, capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise AssertionError("MySQL verification query failed: " + result.stderr)
    return list(csv.DictReader(io.StringIO(result.stdout), delimiter="\t"))

def create_exam(session, course_id, strategy):
    title = "[QA " + RUN + "] " + strategy
    start = datetime.now().replace(hour=8, minute=0, second=0, microsecond=0) + timedelta(days=2)
    fields = {
        "CourseId": course_id, "Title": title, "StartsAt": start.isoformat(timespec="minutes"),
        "EndsAt": (start + timedelta(hours=3)).isoformat(timespec="minutes"),
        "DurationMinutesPerStudent": 15, "MainQuestionCount": 3, "MaxFollowUpPerQuestion": 2,
        "SelectionStrategy": strategy, "AvoidRecentDuplicateCount": 1
    }
    status, _, headers = session.post("/Exams/Create", fields, redirect=False)
    assert status == 302, ("create", status)
    exam_id = int(headers["Location"].rstrip("/").split("/")[-1])
    return exam_id, title, fields

def draft_post(session, exam_id, action, fields=None):
    return session.post("/Exams/" + action + "/" + str(exam_id), fields or {},
                        token_path="/Exams/Details/" + str(exam_id))

anonymous = Session()
status, _, headers = anonymous.request("/Exams", redirect=False)
check(status in (302, 401), "Anonymous cannot manage exams")
lecturer = Session()
check(lecturer.login("lecturer1@aives.local") == "/Exams", "Lecturer login redirects to management")
for email in ["lecturer2@aives.local"] + [f"student{i:02}@aives.local" for i in range(1, 9)]:
    demo = Session()
    destination = demo.login(email)
    check(destination == ("/Exams" if email.startswith("lecturer") else "/Student/MySchedule"), "Demo account login: " + email)

new_student = Session()
email = "qa." + RUN + "@aives.local"
registration = {"FullName": "QA Student " + RUN, "Email": email, "StudentCode": "QA" + RUN,
                "Password": "Strong@123", "ConfirmPassword": "Strong@123", "Role": "ADMIN"}
status, _, headers = new_student.post("/Account/Register", registration, redirect=False)
check(status == 302 and headers["Location"] == "/Account/Login", "Register succeeds")
new_student.login(email, "Strong@123")
identity = new_student.api("/api/users/me")
check(identity["roles"] == ["STUDENT"], "Public registration ignores injected ADMIN role")
status, _, _ = new_student.request("/Exams")
check(status == 403, "Student cannot access Lecturer management")
status, body, _ = new_student.request("/Student/MySchedule?studentId=1")
check(status == 200 and "Published Demo" not in body, "StudentId query cannot expose another student's schedule")
invalid_registration = Session()
bad = dict(registration, Email="weak." + RUN + "@aives.local", StudentCode="WEAK" + RUN, Password="12345678", ConfirmPassword="12345678")
status, body, _ = invalid_registration.post("/Account/Register", bad)
check(status == 200 and "validation-summary-errors" in body, "Weak password rejected")
status, body, _ = invalid_registration.post("/Account/Register", registration)
check(status == 200 and "Email đã được đăng ký" in body, "Duplicate email rejected")
status, _, _ = lecturer.request("/Exams/GenerateSchedule/1", {}, redirect=False)
check(status == 400, "POST without antiforgery token rejected")

course = next(c for c in lecturer.api("/api/courses") if c["courseCode"] == "PRN222")
eid, title, fields = create_exam(lecturer, course["courseId"], "RANDOM")
check(eid > 0, "Lecturer creates exam in assigned course")
other = Session()
other.login("lecturer2@aives.local")
status, _, _ = other.request("/Exams/Details/" + str(eid))
check(status == 403, "Other lecturer cannot inspect owned exam")
status, _, _ = other.post("/Exams/Edit/" + str(eid), fields, token_path="/Exams/Create")
unchanged = sql(f"SELECT title,selection_strategy FROM exams WHERE ExamId={eid}")[0]
check(unchanged["title"] == title and unchanged["selection_strategy"] == "RANDOM", "Other lecturer cannot modify owned exam")
foreign_course = next(c for c in other.api("/api/courses") if c["courseCode"] == "SWP391")
status, body, _ = lecturer.post("/Exams/Create", dict(fields, CourseId=foreign_course["courseId"], Title="Forbidden"))
check(status == 200 and "không phụ trách" in body, "Creating exam for unassigned course rejected")
status, body, _ = lecturer.post("/Exams/Create", dict(fields, DurationMinutesPerStudent=0))
check(status == 200 and "validation-summary-errors" in body, "Invalid interval rejected")
status, _, headers = lecturer.post("/Exams/Edit/" + str(eid), dict(fields, Description="QA edit"), redirect=False)
check(status == 302, "Lecturer edits DRAFT exam")

detail_path = "/Exams/Details/" + str(eid)
_, detail_html, _ = lecturer.request(detail_path)
student_ids = re.findall(r'name="StudentIds" value="(\d+)"', detail_html)
question_ids = re.findall(r'name="QuestionIds" value="(\d+)"', detail_html)
check(len(student_ids) >= 8 and len(question_ids) >= 12, "Seeder supplies enrolled students and approved question pool")
status, body, _ = draft_post(lecturer, eid, "Participants", [("StudentIds", student_ids[0]), ("StudentIds", student_ids[0])])
check("bị trùng" in body, "Duplicate participant rejected")
status, body, _ = draft_post(lecturer, eid, "Participants", {"StudentIds": identity["userId"]})
check("phải đang hoạt động" in body, "Non-enrolled student rejected")
draft_post(lecturer, eid, "Participants", [("StudentIds", i) for i in student_ids[:5]])
status, body, _ = draft_post(lecturer, eid, "Publish")
check("Đã chọn 0 câu" in body, "Publish without question pool rejected; schedule is already automatic")
status, body, _ = draft_post(lecturer, eid, "GenerateSchedule")
check("Đã tự xếp" in body, "Generate Schedule succeeds")
slots = sql(f"SELECT id,scheduled_start_at,scheduled_end_at FROM exam_participants WHERE exam_id={eid} ORDER BY scheduled_start_at")
check(len(slots) == 5 and all(slots[i-1]["scheduled_end_at"] <= slots[i]["scheduled_start_at"] for i in range(1, len(slots))), "Schedule slots do not overlap")
manual = []
for i, slot in enumerate(slots):
    manual.extend([(f"Slots[{i}].ParticipantId", slot["id"]), (f"Slots[{i}].Start", slot["scheduled_start_at"].replace(" ", "T")),
                   (f"Slots[{i}].End", slot["scheduled_end_at"].replace(" ", "T"))])
invalid = list(manual)
invalid[4] = ("Slots[1].Start", slots[0]["scheduled_start_at"].replace(" ", "T"))
status, body, _ = draft_post(lecturer, eid, "SaveSchedule", invalid)
check("chồng lấn" in body, "Overlapping manual slots rejected")
check(sql(f"SELECT id,scheduled_start_at,scheduled_end_at FROM exam_participants WHERE exam_id={eid} ORDER BY scheduled_start_at") == slots, "Invalid schedule leaves stored slots unchanged")
invalid = list(manual)
invalid[2] = ("Slots[0].End", (datetime.fromisoformat(fields["EndsAt"]) + timedelta(minutes=1)).isoformat())
status, body, _ = draft_post(lecturer, eid, "SaveSchedule", invalid)
check("ngoài khung giờ" in body, "Slot beyond exam end rejected")
status, body, _ = draft_post(lecturer, eid, "SaveSchedule", manual)
check("Đã sửa lịch" in body, "Manual valid schedule succeeds")
status, body, _ = draft_post(lecturer, eid, "QuestionPool", {"QuestionIds": question_ids[0]})
check("Đã chọn 1 câu" in body and "Chọn thêm 2 câu" in body, "Partial pool saved with actionable missing-question count")
status, body, _ = draft_post(lecturer, eid, "GenerateAssignments")
check("Đã chọn 1 câu" in body, "Cannot generate assignments from insufficient pool")
status, body, _ = draft_post(lecturer, eid, "QuestionPool", [("QuestionIds", q) for q in question_ids[:3]] + [("QuestionIds", 999999)])
check("đúng môn học" in body, "Invalid pool question rejected")
draft_post(lecturer, eid, "QuestionPool", [("QuestionIds", q) for q in question_ids])
status, body, _ = draft_post(lecturer, eid, "Publish")
check("chưa đầy đủ" in body, "Publish without assignments rejected")
status, body, _ = draft_post(lecturer, eid, "GenerateAssignments")
check("Đã tạo bộ câu hỏi" in body, "RANDOM assignments generated")
def assignment_groups(exam_id):
    rows = sql(f"SELECT a.exam_participant_id,a.question_id,a.sequence_no,p.slot_order FROM exam_question_assignments a JOIN exam_participants p ON p.id=a.exam_participant_id WHERE p.exam_id={exam_id} ORDER BY p.scheduled_start_at,a.sequence_no")
    groups = {}
    for row in rows:
        groups.setdefault(row["exam_participant_id"], []).append(row["question_id"])
    return list(groups.values()), rows
groups, before = assignment_groups(eid)
check(len(groups) == 5 and all(len(g) == len(set(g)) == 3 for g in groups), "Each student receives three unique questions")
check(all(not set(groups[i-1]) & set(groups[i]) for i in range(1, len(groups))), "Consecutive students have no repeated questions with sufficient pool")
status, body, _ = draft_post(lecturer, eid, "Publish")
check("Đã Publish" in body and sql(f"SELECT status FROM exams WHERE ExamId={eid}")[0]["status"] == "PUBLISHED", "Publish succeeds")
status, body, _ = draft_post(lecturer, eid, "GenerateAssignments")
check("Chỉ được thay đổi" in body and assignment_groups(eid)[1] == before, "Published exam cannot silently regenerate assignments")
student = Session()
student.login("student01@aives.local")
status, body, _ = student.request("/Student/MySchedule")
check(title in body, "Student sees newly published own schedule")
check(all(html.escape(q) not in body for q in ["Dependency Injection trong ASP.NET Core là gì?", "Repository Pattern giải quyết vấn đề gì?"]), "Student schedule never exposes question content")
student06 = Session()
student06.login("student06@aives.local")
status, body, _ = student06.request("/Student/MySchedule?studentId=1")
check(title not in body, "Student outside participant list cannot see this exam")
status, _, _ = student.request("/Exams/Details/" + str(eid))
check(status == 403, "Student cannot access assignment preview")

adaptive_id, _, _ = create_exam(lecturer, course["courseId"], "ADAPTIVE")
draft_post(lecturer, adaptive_id, "Participants", [("StudentIds", i) for i in student_ids[:3]])
draft_post(lecturer, adaptive_id, "GenerateSchedule")
draft_post(lecturer, adaptive_id, "QuestionPool", [("QuestionIds", q) for q in question_ids])
draft_post(lecturer, adaptive_id, "GenerateAssignments")
balanced = sql(f"SELECT a.exam_participant_id,q.difficulty FROM exam_question_assignments a JOIN questions q ON q.id=a.question_id JOIN exam_participants p ON p.id=a.exam_participant_id WHERE p.exam_id={adaptive_id}")
levels = {}
for row in balanced: levels.setdefault(row["exam_participant_id"], set()).add(row["difficulty"])
check(len(levels) == 3 and all(level == {"Easy", "Medium", "Hard"} for level in levels.values()), "ADAPTIVE balances seeded difficulty metadata")
draft_post(lecturer, adaptive_id, "QuestionPool", [("QuestionIds", q) for q in question_ids[:3]])
status, body, _ = draft_post(lecturer, adaptive_id, "GenerateAssignments")
groups, _ = assignment_groups(adaptive_id)
check(len(groups) == 3 and all(len(set(g)) == 3 for g in groups) and "Lặp gần nhau" in body, "Small pool relaxes recent repetition visibly without internal duplicates")
status, _, _ = new_student.post("/Account/Logout", {}, token_path="/Student/MySchedule", redirect=False)
check(status == 302, "Logout succeeds")
status, _, _ = new_student.request("/Student/MySchedule", redirect=False)
check(status in (302, 401), "Logged-out session cannot access schedule")


# Course and question management, then start-only automatic scheduling.
status, _, headers = lecturer.post("/Courses/Create", {"Code": "QA" + RUN, "Name": "QA course " + RUN, "IsActive": "true"}, redirect=False)
check(status == 302, "Lecturer creates course and becomes its assigned lecturer")
course_id = int(headers["Location"].rstrip("/").split("/")[-1])
status, _, _ = lecturer.request("/Courses/Details/" + str(course_id))
check(status == 200, "New course details and enrollment UI load")
status, _, _ = other.request("/Courses/Edit/" + str(course_id))
check(status == 403, "Unassigned lecturer cannot edit course")
status, _, _ = student.request("/Courses")
check(status == 403, "Student cannot access course management")
status, body, _ = lecturer.post("/Courses/Create", {"Code": "QA" + RUN, "Name": "Duplicate", "IsActive": "true"})
check("Mã môn học đã tồn tại" in body, "Duplicate course code rejected")
status, _, _ = lecturer.post("/Courses/Edit/" + str(course_id), {"Code": "QA" + RUN, "Name": "QA course edited " + RUN, "Description": "Edited description", "IsActive": "true"}, redirect=False)
check(status == 302 and sql(f"SELECT name FROM courses WHERE CourseId={course_id}")[0]["name"] == "QA course edited " + RUN, "Course edits persisted")
course_students = sql("SELECT id FROM users WHERE email IN ('student01@aives.local','student02@aives.local','student03@aives.local','student04@aives.local') ORDER BY email")
status, body, _ = lecturer.post("/Courses/Students/" + str(course_id), [("StudentIds", row["id"]) for row in course_students], token_path="/Courses/Details/" + str(course_id))
check("Đã cập nhật sinh viên" in body, "Course enrollment saves active students")
for index, difficulty in enumerate(["Easy", "Medium", "Hard"]):
    status, body, _ = lecturer.post("/Courses/Question/" + str(course_id), {"Content": "QA question " + RUN + " " + str(index), "Difficulty": difficulty, "BloomLevel": ["Remember", "Understand", "Apply"][index], "IsApproved": "true", "IsActive": "true"})
    check("Đã lưu câu hỏi" in body, "Create approved course question: " + difficulty)
course_questions = sql(f"SELECT id FROM questions WHERE course_id={course_id} ORDER BY id")
qid = int(course_questions[0]["id"])
qpath = "/Courses/Question/" + str(course_id) + "?questionId=" + str(qid)
status, body, _ = lecturer.post(qpath, {"Content": "QA edited question " + RUN, "Difficulty": "Easy", "BloomLevel": "Remember", "IsApproved": "true", "IsActive": "true"})
check("Đã lưu câu hỏi" in body and sql(f"SELECT content FROM questions WHERE id={qid}")[0]["content"] == "QA edited question " + RUN, "Question content edits persisted")
status, body, _ = other.post(qpath, {"Content": "Forbidden change", "Difficulty": "Easy", "IsApproved": "true", "IsActive": "true"}, token_path="/Exams/Create")
check(sql(f"SELECT content FROM questions WHERE id={qid}")[0]["content"] == "QA edited question " + RUN, "Other lecturer cannot mutate course questions")

auto_id, auto_title, auto_fields = create_exam(lecturer, course_id, "RANDOM")
status, body, _ = draft_post(lecturer, auto_id, "Participants", [("StudentIds", row["id"]) for row in course_students])
auto_slots = sql(f"SELECT scheduled_start_at,scheduled_end_at FROM exam_participants WHERE exam_id={auto_id} ORDER BY scheduled_start_at")
check(len(auto_slots) == 4 and all((datetime.fromisoformat(r["scheduled_end_at"]) - datetime.fromisoformat(r["scheduled_start_at"])).total_seconds() == 15 * 60 for r in auto_slots), "Adding participants immediately creates valid 15-minute slots")
new_start = datetime.fromisoformat(auto_fields["StartsAt"]).replace(hour=9)
status, body, _ = draft_post(lecturer, auto_id, "GenerateSchedule", {"StartsAt": new_start.isoformat(timespec="minutes"), "DurationMinutesPerStudent": 20})
auto_slots = sql(f"SELECT scheduled_start_at,scheduled_end_at FROM exam_participants WHERE exam_id={auto_id} ORDER BY scheduled_start_at")
auto_exam = sql(f"SELECT starts_at,ends_at,duration_minutes_per_student FROM exams WHERE ExamId={auto_id}")[0]
check(datetime.fromisoformat(auto_exam["ends_at"]) == new_start + timedelta(minutes=80), "Start and 20-minute interval auto-calculate exam end for four students")
check(all(datetime.fromisoformat(r["scheduled_start_at"]) == new_start + timedelta(minutes=20*i) and datetime.fromisoformat(r["scheduled_end_at"]) == new_start + timedelta(minutes=20*(i+1)) for i,r in enumerate(auto_slots)), "Automatic slots follow 09:00, 09:20, 09:40, 10:00")
status, body, _ = lecturer.request("/Exams/Details/" + str(auto_id))
check('name="Slots[' not in body and "Lưu và xếp lịch tự động" in body, "Schedule UI requires only start and minutes, no individual end-time inputs")
_, form_body, _ = lecturer.request("/Exams/Create")
check('name="EndsAt"' not in form_body, "Exam form no longer asks lecturer to supply end time")
status, body, _ = draft_post(lecturer, auto_id, "QuestionPool", {"QuestionIds": qid})
check("Đã chọn 1 câu" in body and "Chọn thêm 2 câu" in body, "Pool warning explains exact shortage")
draft_post(lecturer, auto_id, "QuestionPool", [("QuestionIds", row["id"]) for row in course_questions])
draft_post(lecturer, auto_id, "GenerateAssignments")
status, body, _ = draft_post(lecturer, auto_id, "Publish")
check("Đã Publish" in body, "New course/question/start-only scheduling flow publishes successfully")
status, body, _ = lecturer.post(qpath, {"Content": "Change published question", "Difficulty": "Easy", "IsApproved": "true", "IsActive": "true"}, token_path="/Exams/Create")
check("giữ nguyên đề đã công bố" in body and sql(f"SELECT content FROM questions WHERE id={qid}")[0]["content"] == "QA edited question " + RUN, "Published exam questions cannot be silently changed")
status, body, _ = student.request("/Student/MySchedule")
check(auto_title in body and "09:00" in body and "09:20" in body, "Student sees correct 20-minute automatic appointment")

report = {"passed": len(CHECKS), "checks": CHECKS, "qaExamIds": [eid, adaptive_id, auto_id], "run": RUN}
Path(os.environ.get("TEMP", "."), "AivesFunction2Acceptance.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Completed: {len(CHECKS)} checks; QA exam IDs {eid}, {adaptive_id}, {auto_id}.")
