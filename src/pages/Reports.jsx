import { useState, useMemo } from "react";
import Sidebar from "../components/Sidebar.jsx";
import "../styles/Dashboard.css";
import "../styles/Exams.css";

const REPORT_METRICS = [
  { label: "Total Exams Conducted", value: "14", delta: "+3 this month", tone: "positive" },
  { label: "Average Viva Score", value: "8.4 / 10", delta: "+0.6 vs last term", tone: "positive" },
  { label: "Grading Agreement", value: "94.2%", delta: "AI & Lecturer aligned", tone: "positive" },
  { label: "Pending Reviews", value: "2", delta: "Needs manual sign-off", tone: "danger" },
];

const EXAM_REPORTS = [
  {
    examId: 1,
    examName: "Midterm Oral Defense - Software Architecture",
    courseCode: "SWD392",
    courseName: "Software Architecture & Design",
    date: "2026-10-20",
    candidatesCount: 4,
    avgAiScore: "8.2",
    avgLecturerScore: "8.5",
    finalAvgScore: "8.4",
    status: "Completed",
  },
  {
    examId: 2,
    examName: "Database Indexing & Query Optimization Viva",
    courseCode: "DBI202",
    courseName: "Database Systems",
    date: "2026-10-15",
    candidatesCount: 6,
    avgAiScore: "7.8",
    avgLecturerScore: "8.0",
    finalAvgScore: "7.9",
    status: "Completed",
  },
  {
    examId: 3,
    examName: "Microservice Communication Defense",
    courseCode: "PRN231",
    courseName: "Cross-Platform Web APIs with .NET",
    date: "2026-10-12",
    candidatesCount: 5,
    avgAiScore: "8.9",
    avgLecturerScore: "8.8",
    finalAvgScore: "8.8",
    status: "Completed",
  },
  {
    examId: 4,
    examName: "Final Oral Defense - Software Patterns",
    courseCode: "SWD392",
    courseName: "Software Architecture & Design",
    date: "2026-10-08",
    candidatesCount: 8,
    avgAiScore: "8.1",
    avgLecturerScore: "8.3",
    finalAvgScore: "8.2",
    status: "Completed",
  },
];

const RUBRIC_CRITERIA_STATS = [
  { criteria: "Technical Accuracy & Core Concepts", score: 8.6, pct: 86 },
  { criteria: "Problem Analysis & Architectural Justification", score: 8.1, pct: 81 },
  { criteria: "Handling Follow-Up & Deep-Dive Questions", score: 7.9, pct: 79 },
  { criteria: "Verbal Articulation & Concise Communication", score: 8.8, pct: 88 },
];

export default function Reports() {
  const [courseFilter, setCourseFilter] = useState("ALL");
  const [searchTerm, setSearchTerm] = useState("");

  const filteredReports = useMemo(() => {
    return EXAM_REPORTS.filter((item) => {
      const matchCourse = courseFilter === "ALL" || item.courseCode === courseFilter;
      const matchSearch =
        item.examName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.courseCode.toLowerCase().includes(searchTerm.toLowerCase());
      return matchCourse && matchSearch;
    });
  }, [courseFilter, searchTerm]);

  const handleExportCSV = () => {
    const headers = ["Exam ID", "Exam Name", "Course", "Date", "Candidates", "Avg AI Score", "Avg Lecturer Score", "Final Score"];
    const rows = filteredReports.map((r) => [
      r.examId,
      `"${r.examName}"`,
      r.courseCode,
      r.date,
      r.candidatesCount,
      r.avgAiScore,
      r.avgLecturerScore,
      r.finalAvgScore,
    ]);
    const csvContent = "data:text/csv;charset=utf-8," + [headers.join(","), ...rows.map((e) => e.join(","))].join("\n");
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `Viva_Exam_Reports_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <Sidebar
      title="Exam & Viva Reports"
      subtitle="Comprehensive performance analytics, candidate grade distribution, and AI evaluation metrics."
      actions={
        <button type="button" className="btn-primary" onClick={handleExportCSV}>
          Export Reports (CSV)
        </button>
      }
    >
      {/* Metric Cards */}
      <section className="stat-strip" aria-label="Key Report Metrics">
        {REPORT_METRICS.map((stat) => (
          <div key={stat.label} className="stat-strip__item">
            <span className="stat-strip__value">{stat.value}</span>
            <span className="stat-strip__label">{stat.label}</span>
            <span className={`stat-strip__delta is-${stat.tone}`}>{stat.delta}</span>
          </div>
        ))}
      </section>

      {/* Main Content Layout */}
      <div className="dash-grid" style={{ gridTemplateColumns: "1.8fr 1fr", gap: "1.5rem" }}>
        {/* Left Side: Exam Reports Table */}
        <div className="panel">
          <div className="panel__head" style={{ flexWrap: "wrap", gap: "0.75rem" }}>
            <div>
              <h2 style={{ margin: 0, fontSize: "1.15rem", fontFamily: "var(--font-display)" }}>
                Concluded Exam Evaluations
              </h2>
              <p style={{ margin: "0.2rem 0 0", fontSize: "0.85rem", color: "var(--ink-text-dim)" }}>
                Overview of recent oral examination sessions with grading reconciliation
              </p>
            </div>

            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input
                type="text"
                placeholder="Search exams..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                style={{
                  padding: "0.45rem 0.75rem",
                  border: "1px solid var(--parchment-line)",
                  borderRadius: "6px",
                  fontSize: "0.85rem",
                  background: "#fff",
                }}
              />
              <select
                value={courseFilter}
                onChange={(e) => setCourseFilter(e.target.value)}
                style={{
                  padding: "0.45rem 0.75rem",
                  border: "1px solid var(--parchment-line)",
                  borderRadius: "6px",
                  fontSize: "0.85rem",
                  background: "#fff",
                }}
              >
                <option value="ALL">All Courses</option>
                <option value="SWD392">SWD392</option>
                <option value="DBI202">DBI202</option>
                <option value="PRN231">PRN231</option>
              </select>
            </div>
          </div>

          <table className="exams-table" style={{ width: "100%", marginTop: "1rem" }}>
            <thead>
              <tr>
                <th>Exam Session</th>
                <th>Candidates</th>
                <th>AI Score</th>
                <th>Lecturer</th>
                <th>Final Score</th>
              </tr>
            </thead>
            <tbody>
              {filteredReports.map((item) => (
                <tr key={item.examId}>
                  <td>
                    <div className="exam-cell-name">
                      <span className="exam-cell-title" style={{ fontSize: "0.95rem" }}>
                        {item.examName}
                      </span>
                      <span className="exam-cell-desc" style={{ fontSize: "0.8rem" }}>
                        [{item.courseCode}] {item.courseName} &bull; {item.date}
                      </span>
                    </div>
                  </td>
                  <td>
                    <strong>{item.candidatesCount}</strong> students
                  </td>
                  <td>
                    <span style={{ fontWeight: 600, color: "var(--ink-text)" }}>{item.avgAiScore}</span> / 10
                  </td>
                  <td>
                    <span style={{ fontWeight: 600, color: "var(--ink-text)" }}>{item.avgLecturerScore}</span> / 10
                  </td>
                  <td>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "0.2rem 0.55rem",
                        borderRadius: "4px",
                        background: "#ecfdf5",
                        color: "#065f46",
                        fontWeight: 700,
                        fontSize: "0.9rem",
                      }}
                    >
                      {item.finalAvgScore}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Right Side: Rubric & Performance Distribution */}
        <div className="dash-side">
          <div className="panel">
            <div className="panel__head">
              <h2 style={{ margin: 0, fontSize: "1.1rem", fontFamily: "var(--font-display)" }}>
                Rubric Criteria Performance
              </h2>
            </div>
            <p style={{ fontSize: "0.85rem", color: "var(--ink-text-dim)", marginTop: 0 }}>
              Average student competency score aggregated across evaluated viva criteria:
            </p>

            <div style={{ display: "flex", flexDirection: "column", gap: "1rem", marginTop: "1rem" }}>
              {RUBRIC_CRITERIA_STATS.map((crit) => (
                <div key={crit.criteria}>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "0.3rem" }}>
                    <span style={{ fontWeight: 500, color: "var(--ink-text)" }}>{crit.criteria}</span>
                    <span style={{ fontWeight: 600, color: "var(--brass-deep)" }}>{crit.score} / 10</span>
                  </div>
                  <div style={{ height: "7px", background: "var(--parchment-deep)", borderRadius: "4px", overflow: "hidden" }}>
                    <div
                      style={{
                        width: `${crit.pct}%`,
                        height: "100%",
                        background: crit.pct >= 85 ? "var(--moss)" : "var(--brass)",
                        borderRadius: "4px",
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="panel" style={{ background: "#fcfaf4", border: "1px dashed var(--parchment-line)" }}>
            <h3 style={{ margin: "0 0 0.5rem", fontSize: "1rem", color: "var(--ink-text)" }}>
              AI Grading Calibration Note
            </h3>
            <p style={{ margin: 0, fontSize: "0.85rem", color: "var(--ink-text-dim)", lineHeight: 1.5 }}>
              Average delta between AI transcription scores and lecturer manual sign-offs is under 0.35 points across all 14 evaluated defense sessions.
            </p>
          </div>
        </div>
      </div>
    </Sidebar>
  );
}
