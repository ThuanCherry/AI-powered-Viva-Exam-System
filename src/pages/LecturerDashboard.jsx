import { Link, Navigate } from 'react-router-dom';
import AppShell from '../components/Sidebar.jsx';
import { getSession } from '../auth/session.js';
import { useApi } from '../hooks/useApi.js';
import { vnTime } from '../lib/time.js';
import { Status } from './Exams.jsx';
import '../styles/Lecturer.css';

export default function LecturerDashboard() {
  const lecturer = getSession()?.role === 'Lecturer';
  const { data, error, loading } = useApi(lecturer ? '/api/lecturer/dashboard' : null);
  if (!lecturer) return <Navigate to="/exams" replace />;
  return <AppShell title="Tổng quan giảng dạy" subtitle="Môn học → Lớp học phần → Sinh viên → Học liệu → Lịch thi" actions={<Link className="wf-button" to="/exams/new">+ Tạo kỳ thi</Link>}><div className="wf-page">
    {error && <p className="wf-error" role="alert">{error}</p>}{loading && <p role="status">Đang tải tổng quan…</p>}
    {data && <>
      <section className="lc-summary">{[['Môn đang dạy', data.courseCount], ['Lớp học phần', data.classCount], ['Sinh viên', data.studentCount], ['Kỳ thi sắp tới', data.upcomingCount], ['Câu hỏi', data.questionCount], ['Tài liệu', data.documentCount]].map(([label, value]) => <div key={label}><strong>{value}</strong><span>{label}</span></div>)}</section>
      <nav className="lc-shortcuts" aria-label="Thao tác nhanh">{[['/courses', 'Môn học'], ['/classes', 'Quản lý lớp'], ['/classes', 'Sinh viên'], ['/documents', 'Tài liệu'], ['/questions', 'Ngân hàng câu hỏi']].map(([to, label]) => <Link className="wf-button wf-secondary" key={label} to={to}>{label} →</Link>)}</nav>
      <section className="wf-panel"><div className="wf-row"><h2>Kỳ thi sắp tới</h2><Link className="wf-back" to="/exams">Xem tất cả →</Link></div>
        {data.upcoming.length === 0 && <p className="wf-muted">Chưa có lịch thi được công bố. Bắt đầu bằng việc chọn môn và tạo lớp học phần.</p>}
        {data.upcoming.map(e => <article className="lc-list-row" key={e.examId}><div><Link to={`/exams/${e.examId}`}><strong>{e.examName}</strong></Link><small>{e.courseName} · {vnTime(e.startTime)}</small></div><Status value={e.status} /></article>)}
      </section>
    </>}
  </div></AppShell>;
}
