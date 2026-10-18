import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import Sidebar from "../components/Sidebar.jsx";
import CreateExamModal from "../components/CreateExamModal.jsx";
import { getExams, updateExamStatus } from "../api/exams.js";
import "../styles/Exams.css";

function StatusBadge({ status }) {
  const norm = (status ?? "Draft").toLowerCase();
  return <span className={`badge-status is-${norm}`}>{status}</span>;
}

export default function Exams() {
  const navigate = useNavigate();
  const [exams, setExams] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [isModalOpen, setIsModalOpen] = useState(false);

  const fetchExams = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getExams();
      setExams(data ?? []);
    } catch (err) {
      setError(err.message || "Failed to load exams.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchExams();
  }, []);

  const filteredExams = useMemo(() => {
    const term = search.toLowerCase().trim();
    return exams.filter((e) => {
      const matchSearch =
        !term ||
        e.examName?.toLowerCase().includes(term) ||
        e.courseName?.toLowerCase().includes(term) ||
        e.courseCode?.toLowerCase().includes(term);

      const matchStatus =
        statusFilter === "ALL" || e.status?.toUpperCase() === statusFilter.toUpperCase();

      return matchSearch && matchStatus;
    });
  }, [exams, search, statusFilter]);

  const handleCreateSuccess = (created) => {
    setIsModalOpen(false);
    fetchExams();
    if (created?.examId) {
      navigate(`/exams/${created.examId}/config`);
    }
  };

  const handleCancelExam = async (examId) => {
    if (!window.confirm("Are you sure you want to cancel this exam session?")) return;
    try {
      await updateExamStatus(examId, "Cancelled");
      fetchExams();
    } catch (err) {
      alert(err.message || "Failed to cancel exam.");
    }
  };

  return (
    <Sidebar
      title="Exam Management"
      subtitle="Organize viva exam sessions, configure question pools, and manage candidate schedules."
      actions={
        <button
          type="button"
          className="qc-btn qc-btn--primary"
          onClick={() => setIsModalOpen(true)}
        >
          + New exam
        </button>
      }
    >
      <div className="exams-toolbar">
        <div className="exams-filters">
          <input
            type="text"
            className="exams-search"
            placeholder="Search by exam name or course..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />

          <select
            className="exams-select"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="ALL">All Statuses</option>
            <option value="DRAFT">Draft</option>
            <option value="SCHEDULED">Scheduled</option>
            <option value="READY">Ready</option>
            <option value="INPROGRESS">In Progress</option>
            <option value="COMPLETED">Completed</option>
            <option value="CANCELLED">Cancelled</option>
          </select>
        </div>

        <button type="button" className="qc-btn qc-btn--ghost" onClick={fetchExams}>
          Refresh
        </button>
      </div>

      {error && <div className="modal-error" style={{ marginBottom: "1rem" }}>{error}</div>}

      <div className="exams-table-wrap">
        {loading ? (
          <div className="exams-empty">
            <p>Loading exam sessions...</p>
          </div>
        ) : filteredExams.length === 0 ? (
          <div className="exams-empty">
            <p>No viva exam sessions found.</p>
            <button
              type="button"
              className="qc-btn qc-btn--primary"
              onClick={() => setIsModalOpen(true)}
            >
              + Create your first exam
            </button>
          </div>
        ) : (
          <table className="exams-table">
            <thead>
              <tr>
                <th>Exam Name & Course</th>
                <th>Status</th>
                <th>Candidates</th>
                <th>Schedule Window</th>
                <th>Duration / Student</th>
                <th style={{ textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredExams.map((exam) => (
                <tr key={exam.examId}>
                  <td>
                    <div className="exam-cell-name">
                      <span className="exam-cell-title">{exam.examName}</span>
                      <span className="exam-cell-desc">
                        [{exam.courseCode || "N/A"}] {exam.courseName || "Course"} &bull; Lecturer: {exam.lecturerName || "Assigned"}
                      </span>
                    </div>
                  </td>
                  <td>
                    <StatusBadge status={exam.status} />
                  </td>
                  <td>
                    <strong>{exam.candidateCount ?? 0}</strong> students
                  </td>
                  <td>
                    {new Date(exam.startTime).toLocaleDateString()}{" "}
                    {new Date(exam.startTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                  </td>
                  <td>{exam.timePerStudent} mins</td>
                  <td>
                    <div className="exams-actions" style={{ justifyContent: "flex-end" }}>
                      <Link
                        to={`/exams/${exam.examId}/config`}
                        className="action-btn-link"
                      >
                        Configure & Schedule
                      </Link>

                      {exam.status !== "Cancelled" && exam.status !== "Completed" && (
                        <button
                          type="button"
                          className="action-btn-cancel"
                          onClick={() => handleCancelExam(exam.examId)}
                        >
                          Cancel
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <CreateExamModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSuccess={handleCreateSuccess}
      />
    </Sidebar>
  );
}
