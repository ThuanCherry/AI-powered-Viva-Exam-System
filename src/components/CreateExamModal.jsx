import { useEffect, useState } from "react";
import { getCourses } from "../api/courses.js";
import { createExam } from "../api/exams.js";
import { getSession } from "../auth/session.js";
import "../styles/CreateExamModal.css";

export default function CreateExamModal({ isOpen, onClose, onSuccess }) {
  const [courses, setCourses] = useState([]);
  const [loadingCourses, setLoadingCourses] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  // Default times: starting tomorrow 08:00 to 17:00
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const defaultStart = new Date(tomorrow.setHours(8, 0, 0, 0)).toISOString().slice(0, 16);
  const defaultEnd = new Date(tomorrow.setHours(17, 0, 0, 0)).toISOString().slice(0, 16);

  const [form, setForm] = useState({
    courseId: "",
    examName: "",
    description: "",
    startTime: defaultStart,
    endTime: defaultEnd,
    timePerStudent: 20,
    mainQuestionCount: 3,
    maxFollowUpCount: 2,
    secondsPerQuestion: 120,
  });

  useEffect(() => {
    if (!isOpen) return;
    const session = getSession();

    getCourses()
      .then((data) => {
        let available = data ?? [];
        // Nếu người dùng là Giảng viên (Lecturer), chỉ hiển thị môn học được phân công cho chính họ
        if (session?.role === "Lecturer" && session?.userId) {
          available = available.filter(
            (c) => String(c.lecturerId) === String(session.userId)
          );
        }
        setCourses(available);
        if (available.length > 0) {
          setForm((prev) => ({ ...prev, courseId: available[0].courseId }));
        } else {
          setForm((prev) => ({ ...prev, courseId: "" }));
        }
      })
      .catch((err) => setError(err.message || "Failed to load courses."))
      .finally(() => setLoadingCourses(false));
  }, [isOpen]);

  if (!isOpen) return null;

  const handleChange = (field) => (e) => {
    setForm((prev) => ({ ...prev, [field]: e.target.value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);

    if (!form.courseId) {
      setError("Please select a course.");
      return;
    }
    if (!form.examName.trim()) {
      setError("Please enter the exam name.");
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        courseId: Number(form.courseId),
        examName: form.examName.trim(),
        description: form.description.trim() || null,
        startTime: new Date(form.startTime).toISOString(),
        endTime: new Date(form.endTime).toISOString(),
        timePerStudent: Number(form.timePerStudent),
        mainQuestionCount: Number(form.mainQuestionCount),
        maxFollowUpCount: Number(form.maxFollowUpCount),
        secondsPerQuestion: Number(form.secondsPerQuestion),
      };

      const result = await createExam(payload);
      const created = result?.data ?? result;
      onSuccess(created);
    } catch (err) {
      setError(err.message || "Failed to create exam session.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-dialog" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Create Viva Exam Session</h2>
          <button type="button" className="modal-close" onClick={onClose} aria-label="Close">
            &times;
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            {error && <div className="modal-error">{error}</div>}

            <div className="form-group form-group--full">
              <label htmlFor="courseId">Associated Course *</label>
              {loadingCourses ? (
                <p className="form-hint">Loading courses...</p>
              ) : (
                <select
                  id="courseId"
                  value={form.courseId}
                  onChange={handleChange("courseId")}
                  required
                >
                  <option value="">-- Select a course --</option>
                  {courses.map((c) => (
                    <option key={c.courseId} value={c.courseId}>
                      [{c.courseCode}] {c.courseName}
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div className="form-group form-group--full">
              <label htmlFor="examName">Exam Name *</label>
              <input
                id="examName"
                type="text"
                placeholder="e.g. Oral Defense - Final Term 2026"
                value={form.examName}
                onChange={handleChange("examName")}
                required
              />
            </div>

            <div className="form-group form-group--full">
              <label htmlFor="description">Description</label>
              <textarea
                id="description"
                placeholder="Optional notes or instructions for the viva session..."
                value={form.description}
                onChange={handleChange("description")}
              />
            </div>

            <div className="form-grid">
              <div className="form-group">
                <label htmlFor="startTime">Start Date & Time</label>
                <input
                  id="startTime"
                  type="datetime-local"
                  value={form.startTime}
                  onChange={handleChange("startTime")}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="endTime">End Date & Time</label>
                <input
                  id="endTime"
                  type="datetime-local"
                  value={form.endTime}
                  onChange={handleChange("endTime")}
                  required
                />
              </div>
            </div>

            <div className="form-grid">
              <div className="form-group">
                <label htmlFor="timePerStudent">Duration per candidate (mins)</label>
                <input
                  id="timePerStudent"
                  type="number"
                  min="5"
                  max="120"
                  value={form.timePerStudent}
                  onChange={handleChange("timePerStudent")}
                  required
                />
                <p className="form-hint">Allocated slot per student</p>
              </div>

              <div className="form-group">
                <label htmlFor="secondsPerQuestion">Answer time per question (secs)</label>
                <input
                  id="secondsPerQuestion"
                  type="number"
                  min="15"
                  max="600"
                  value={form.secondsPerQuestion}
                  onChange={handleChange("secondsPerQuestion")}
                  required
                />
                <p className="form-hint">Viva countdown limit</p>
              </div>
            </div>

            <div className="form-grid">
              <div className="form-group">
                <label htmlFor="mainQuestionCount">Main questions per candidate</label>
                <input
                  id="mainQuestionCount"
                  type="number"
                  min="1"
                  max="15"
                  value={form.mainQuestionCount}
                  onChange={handleChange("mainQuestionCount")}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="maxFollowUpCount">Max follow-up (deep-dive) count</label>
                <input
                  id="maxFollowUpCount"
                  type="number"
                  min="0"
                  max="5"
                  value={form.maxFollowUpCount}
                  onChange={handleChange("maxFollowUpCount")}
                  required
                />
              </div>
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="qc-btn qc-btn--ghost" onClick={onClose} disabled={submitting}>
              Cancel
            </button>
            <button type="submit" className="qc-btn qc-btn--primary" disabled={submitting}>
              {submitting ? "Creating..." : "Create & Configure"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
