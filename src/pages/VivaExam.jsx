import { getSession } from "../auth/session.js";
import Sidebar from "../components/Sidebar.jsx";
import "../styles/Dashboard.css";
import "../styles/Exams.css";

const UPCOMING_VIVA_EXAMS = [
  {
    examId: 1,
    examName: "Midterm Oral Defense - Software Architecture",
    courseCode: "SWD392",
    courseName: "Software Architecture & Design",
    date: "2026-10-20",
    time: "10:30 AM",
    duration: "20 minutes",
    examiner: "Dr. Tran Van A",
    status: "Ready",
    canJoin: true,
  },
  {
    examId: 2,
    examName: "Database Systems Oral Examination",
    courseCode: "DBI202",
    courseName: "Database Systems",
    date: "2026-10-28",
    time: "02:00 PM",
    duration: "20 minutes",
    examiner: "Dr. Tran Van A",
    status: "Scheduled",
    canJoin: false,
  },
];

export default function VivaExam() {
  const session = getSession();
  const studentName = session?.fullName || "Student";

  const handleJoin = (examName) => {
    alert(`Connecting to viva defense room for "${examName}". Please ensure your camera and microphone are enabled.`);
  };

  return (
    <Sidebar
      title="Viva Examination Portal"
      subtitle="View your scheduled viva defense sessions, examine room access, and oral guidelines."
    >
      {/* Student Welcome Banner */}
      <div
        className="panel"
        style={{
          background: "linear-gradient(135deg, #10192c 0%, #1e293b 100%)",
          color: "#fff",
          marginBottom: "2rem",
          padding: "1.75rem 2rem",
          borderRadius: "12px",
        }}
      >
        <h2 style={{ margin: "0 0 0.5rem", fontFamily: "var(--font-display)", fontSize: "1.4rem", color: "#f8fafc" }}>
          Welcome back, {studentName}
        </h2>
        <p style={{ margin: 0, color: "#cbd5e1", fontSize: "0.95rem", maxWidth: "40rem", lineHeight: 1.5 }}>
          You are currently signed in as a candidate. Review your assigned oral defense timeslots below and join your examination room on schedule.
        </p>
      </div>

      {/* Upcoming Exams Cards */}
      <h2 style={{ fontFamily: "var(--font-display)", fontSize: "1.25rem", margin: "0 0 1rem" }}>
        Your Scheduled Viva Examinations
      </h2>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(340px, 1fr))", gap: "1.5rem", marginBottom: "2.5rem" }}>
        {UPCOMING_VIVA_EXAMS.map((exam) => (
          <div
            key={exam.examId}
            className="panel"
            style={{
              display: "flex",
              flexDirection: "column",
              justifyContent: "space-between",
              borderRadius: "10px",
              boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
            }}
          >
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "0.75rem" }}>
                <span
                  style={{
                    fontSize: "0.78rem",
                    fontWeight: 700,
                    textTransform: "uppercase",
                    letterSpacing: "0.04em",
                    color: "var(--brass-deep)",
                    background: "#fef3c7",
                    padding: "0.2rem 0.5rem",
                    borderRadius: "4px",
                  }}
                >
                  {exam.courseCode}
                </span>
                <span
                  className={`badge-status is-${exam.status.toLowerCase()}`}
                  style={{ fontSize: "0.75rem", padding: "0.25rem 0.6rem" }}
                >
                  {exam.status}
                </span>
              </div>

              <h3 style={{ margin: "0 0 0.5rem", fontSize: "1.1rem", color: "var(--ink-text)" }}>
                {exam.examName}
              </h3>
              <p style={{ margin: "0 0 1rem", fontSize: "0.85rem", color: "var(--ink-text-dim)" }}>
                {exam.courseName}
              </p>

              <div style={{ display: "flex", flexDirection: "column", gap: "0.4rem", fontSize: "0.85rem", color: "var(--ink-text)", background: "#fffdf9", padding: "0.75rem", borderRadius: "6px", border: "1px solid var(--parchment-line)" }}>
                <div><strong>Date:</strong> {exam.date} at {exam.time}</div>
                <div><strong>Duration:</strong> {exam.duration}</div>
                <div><strong>Lead Examiner:</strong> {exam.examiner}</div>
              </div>
            </div>

            <div style={{ marginTop: "1.5rem" }}>
              {exam.canJoin ? (
                <button
                  type="button"
                  className="btn-primary"
                  style={{ width: "100%", padding: "0.75rem", background: "var(--moss)", color: "#fff" }}
                  onClick={() => handleJoin(exam.examName)}
                >
                  Enter Examination Room
                </button>
              ) : (
                <button
                  type="button"
                  className="btn-primary"
                  style={{ width: "100%", padding: "0.75rem", background: "#94a3b8", cursor: "not-allowed" }}
                  disabled
                >
                  Room Opens on Scheduled Time
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Guidelines Section */}
      <div className="panel" style={{ background: "#fffdf9" }}>
        <h3 style={{ margin: "0 0 0.75rem", fontSize: "1.05rem", fontFamily: "var(--font-display)" }}>
          Viva Defense Instructions for Candidates
        </h3>
        <ul style={{ margin: 0, paddingLeft: "1.25rem", fontSize: "0.88rem", color: "var(--ink-text-dim)", lineHeight: 1.7 }}>
          <li>Please join the oral examination session at least 5 minutes prior to your scheduled slot.</li>
          <li>Ensure your microphone and webcam are working and maintain a quiet, well-lit environment.</li>
          <li>The AI Viva assistant will transcribe your speech in real time for panel verification and rubric scoring.</li>
          <li>Listen carefully to follow-up questions from both the AI and course lecturers.</li>
        </ul>
      </div>
    </Sidebar>
  );
}
