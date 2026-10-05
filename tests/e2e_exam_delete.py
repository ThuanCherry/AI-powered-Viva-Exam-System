"""Verify exam deletion against the running Development server and real MySQL.

Creates and deletes only exams created by this test.
Run from the repository root: python tests/e2e_exam_delete.py
"""
import ast
from pathlib import Path


# Reuse the HTTP/MySQL helpers without executing the broader acceptance flow.
source = ast.parse(Path("tests/e2e_function2.py").read_text(encoding="utf-8-sig"))
helpers = []
for node in source.body:
    if isinstance(node, ast.Assign) and any(
        isinstance(target, ast.Name) and target.id == "anonymous" for target in node.targets
    ):
        break
    helpers.append(node)
exec(compile(ast.Module(body=helpers, type_ignores=[]), "tests/e2e_function2.py", "exec"))

lecturer = Session()
lecturer.login("lecturer1@aives.local")
other = Session()
other.login("lecturer2@aives.local")
student = Session()
student.login("student01@aives.local")
course = next(c for c in lecturer.api("/api/courses") if c["courseCode"] == "PRN222")

for publish in (False, True):
    eid, title, _ = create_exam(lecturer, course["courseId"], "RANDOM")
    path = "/Exams/Delete/" + str(eid)
    _, detail, _ = lecturer.request("/Exams/Details/" + str(eid))
    students = re.findall(r'name="StudentIds" value="(\d+)"', detail)
    questions = re.findall(r'name="QuestionIds" value="(\d+)"', detail)
    draft_post(lecturer, eid, "Participants", [("StudentIds", s) for s in students[:2]])
    draft_post(lecturer, eid, "QuestionPool", [("QuestionIds", q) for q in questions[:6]])
    draft_post(lecturer, eid, "GenerateAssignments")
    participants = sql(f"SELECT id FROM exam_participants WHERE exam_id={eid}")
    ids = ",".join(p["id"] for p in participants)
    check(len(participants) == 2 and len(sql(
        f"SELECT id FROM exam_question_assignments WHERE exam_participant_id IN ({ids})"
    )) == 6, "Deletion fixture has participants, schedule, pool and assignments")
    if publish:
        draft_post(lecturer, eid, "Publish")
        check(sql(f"SELECT status FROM exams WHERE ExamId={eid}")[0]["status"] == "PUBLISHED",
              "Deletion fixture is published")

    status, page, _ = lecturer.request("/Exams")
    check(status == 200 and f'data-exam-title="{title}"' in page and '>Delete</button>' in page,
          "Management page renders Delete form")
    status, _, _ = lecturer.request(path)
    check(status == 405, "GET cannot delete an exam")
    status, _, _ = lecturer.request(path, {}, redirect=False)
    check(status == 400, "Deletion without antiforgery token rejected")
    other.post(path, {}, token_path="/Exams", redirect=False)
    status, _, _ = student.post(path, {}, token_path="/Student/MySchedule", redirect=False)
    check(status in (302, 403), "Student cannot delete an exam")
    check(len(sql(f"SELECT ExamId FROM exams WHERE ExamId={eid}")) == 1
          and len(sql(f"SELECT id FROM exam_participants WHERE exam_id={eid}")) == 2
          and len(sql(f"SELECT id FROM exam_question_assignments WHERE exam_participant_id IN ({ids})")) == 6,
          "Unauthorized deletion preserves exam and dependent records")

    status, _, headers = lecturer.post(path, {}, token_path="/Exams", redirect=False)
    check(status == 302 and headers["Location"] == "/Exams", "Owner deletion returns to management")
    check(not sql(f"SELECT ExamId FROM exams WHERE ExamId={eid}")
          and not sql(f"SELECT id FROM exam_participants WHERE exam_id={eid}")
          and not sql(f"SELECT exam_id FROM exam_question_pool WHERE exam_id={eid}")
          and not sql(f"SELECT id FROM exam_question_assignments WHERE exam_participant_id IN ({ids})"),
          "Exam and all dependent records deleted from MySQL")
    _, page, _ = lecturer.request("/Exams")
    check("Đã xóa kỳ thi." in page and title not in page, "Success message shown and deleted exam removed")
    status, _, _ = lecturer.post(path, {}, token_path="/Exams", redirect=False)
    check(status == 302, "Repeated deletion handled without server error")

print(f"Passed {len(CHECKS)} deletion checks.")
