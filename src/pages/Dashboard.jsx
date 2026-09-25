import Sidebar from "../components/Sidebar.jsx";
import "../styles/Dashboard.css";

const STATS = [
  { label: "Active exams", value: "6", delta: "+2 this week", tone: "positive" },
  { label: "Viva sessions today", value: "4", delta: "2 in progress", tone: "neutral" },
  { label: "Questions in bank", value: "184", delta: "+12 this month", tone: "positive" },
  { label: "Pending grading", value: "9", delta: "3 overdue", tone: "danger" },
];

const SESSIONS = [
  {
    student: "Nguyen Minh Anh",
    exam: "Software Engineering Capstone",
    panel: "Dr. Tran, Dr. Le",
    time: "09:00",
    status: "In Progress",
  },
  {
    student: "Pham Gia Han",
    exam: "Database Systems",
    panel: "Dr. Nguyen",
    time: "10:30",
    status: "Scheduled",
  },
  {
    student: "Le Duc Thinh",
    exam: "Software Engineering Capstone",
    panel: "Dr. Tran",
    time: "13:00",
    status: "Scheduled",
  },
  {
    student: "Vo Thi Kim Ngan",
    exam: "Network Security",
    panel: "Dr. Hoang, AI Panel",
    time: "14:30",
    status: "Scheduled",
  },
  {
    student: "Tran Quoc Bao",
    exam: "Database Systems",
    panel: "Dr. Nguyen",
    time: "Yesterday, 16:00",
    status: "Overdue",
  },
];

const STATUS_BREAKDOWN = [
  { label: "Scheduled", count: 12, tone: "neutral" },
  { label: "In Progress", count: 4, tone: "brass" },
  { label: "Completed", count: 6, tone: "positive" },
  { label: "Overdue", count: 2, tone: "danger" },
];

const GRADING_QUEUE = [
  { student: "Dang Thu Trang", exam: "AI & Machine Learning", when: "Completed 2h ago" },
  { student: "Bui Van Phat", exam: "Software Engineering Capstone", when: "Completed yesterday" },
  { student: "Ngo Hai Yen", exam: "Database Systems", when: "Completed yesterday" },
];

function StatusPill({ status }) {
  const toneClass =
    {
      "In Progress": "is-brass",
      Scheduled: "is-neutral",
      Completed: "is-positive",
      Overdue: "is-danger",
    }[status] || "is-neutral";
  return <span className={`pill ${toneClass}`}>{status}</span>;
}

export default function Dashboard() {
  const maxCount = Math.max(...STATUS_BREAKDOWN.map((s) => s.count));

  return (
    <Sidebar
      title="Dashboard"
      subtitle="Here's what's happening across your exams today."
      actions={
        <button type="button" className="btn-primary">
          + New exam
        </button>
      }
    >
      <section className="stat-strip">
        {STATS.map((stat) => (
          <div className="stat-strip__item" key={stat.label}>
            <span className="stat-strip__value">{stat.value}</span>
            <span className="stat-strip__label">{stat.label}</span>
            <span className={`stat-strip__delta is-${stat.tone}`}>{stat.delta}</span>
          </div>
        ))}
      </section>

      <div className="dash-grid">
        <section className="panel">
          <div className="panel__head">
            <h2>Upcoming viva sessions</h2>
            <a href="#viva" className="panel__link">
              View all
            </a>
          </div>

          <div className="session-list">
            <div className="session-list__row session-list__row--head" aria-hidden="true">
              <span>Student</span>
              <span>Exam</span>
              <span>Panel</span>
              <span>Time</span>
              <span>Status</span>
            </div>
            {SESSIONS.map((s) => (
              <div className="session-list__row" key={s.student}>
                <span className="session-list__student">{s.student}</span>
                <span>{s.exam}</span>
                <span className="session-list__muted">{s.panel}</span>
                <span className="session-list__muted">{s.time}</span>
                <StatusPill status={s.status} />
              </div>
            ))}
          </div>
        </section>

        <div className="dash-side">
          <section className="panel">
            <div className="panel__head">
              <h2>Exam status</h2>
            </div>
            <div className="breakdown">
              {STATUS_BREAKDOWN.map((s) => (
                <div className="breakdown__row" key={s.label}>
                  <span className="breakdown__label">{s.label}</span>
                  <div className="breakdown__track">
                    <div
                      className={`breakdown__fill is-${s.tone}`}
                      style={{ width: `${(s.count / maxCount) * 100}%` }}
                    />
                  </div>
                  <span className="breakdown__count">{s.count}</span>
                </div>
              ))}
            </div>
          </section>

          <section className="panel">
            <div className="panel__head">
              <h2>Grading queue</h2>
              <span className="panel__badge">{GRADING_QUEUE.length}</span>
            </div>
            <ul className="queue-list">
              {GRADING_QUEUE.map((item) => (
                <li className="queue-list__item" key={item.student}>
                  <div>
                    <span className="queue-list__student">{item.student}</span>
                    <span className="queue-list__exam">{item.exam}</span>
                    <span className="queue-list__when">{item.when}</span>
                  </div>
                  <a href="#review" className="queue-list__review">
                    Review
                  </a>
                </li>
              ))}
            </ul>
          </section>
        </div>
      </div>
    </Sidebar>
  );
}