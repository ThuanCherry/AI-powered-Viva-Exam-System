import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import AppShell from '../components/Sidebar.jsx';
import { api } from '../api/client.js';
import { useApi } from '../hooks/useApi.js';
import { getSession } from '../auth/session.js';
import { vnTime } from '../lib/time.js';
import { Status } from './Exams.jsx';
import '../styles/Lecturer.css';

const courseEmpty = { courseCode: '', courseName: '', description: '' };
export function CoursesPage() {
  const { data, error: loadError, loading, reload } = useApi('/api/Courses');
  const [form, setForm] = useState(courseEmpty);
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const save = async e => {
    e.preventDefault(); setBusy(true); setError('');
    try { await (editing ? api.put(`/api/lecturer/courses/${editing}`, form) : api.post('/api/lecturer/courses', form)); setForm(courseEmpty); setEditing(null); setMessage('Đã lưu môn học. Bạn có thể tạo lớp học phần cho môn này.'); reload(); }
    catch (err) { setError(err.message); } finally { setBusy(false); }
  };
  return <AppShell title="Môn học" subtitle="Chọn môn đã có hoặc thêm môn bạn phụ trách"><div className="wf-page">
    {(error || loadError) && <p className="wf-error" role="alert">{error || loadError}</p>}{message && <p className="wf-notice" role="status">{message}</p>}
    <div className="lc-columns"><section className="wf-panel"><h2>{editing ? 'Chỉnh sửa môn học' : 'Thêm môn học'}</h2><form onSubmit={save}><fieldset disabled={busy}>
      <label>Mã môn<input required maxLength={50} value={form.courseCode} onChange={e => setForm({ ...form, courseCode: e.target.value })} /></label>
      <label>Tên môn<input required maxLength={200} value={form.courseName} onChange={e => setForm({ ...form, courseName: e.target.value })} /></label>
      <label>Mô tả<textarea maxLength={1000} value={form.description || ''} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
      <div className="wf-tools"><button className="wf-button">{busy ? 'Đang lưu…' : 'Lưu môn học'}</button>{editing && <button className="wf-button wf-secondary" type="button" onClick={() => { setEditing(null); setForm(courseEmpty); }}>Hủy chỉnh sửa</button>}</div>
    </fieldset></form></section><section className="wf-panel"><h2>Môn đang phụ trách</h2>{loading && <p>Đang tải…</p>}{data?.length === 0 && <p>Chưa có môn học. Thêm môn để bắt đầu quản lý lớp.</p>}
      {data?.map(c => <article className="lc-list-row" key={c.courseId}><div><strong>{c.courseCode} · {c.courseName}</strong><small>{c.description}</small><Link to={`/classes?courseId=${c.courseId}`}>Lớp học phần →</Link></div><button className="lc-text-button" onClick={() => { setEditing(c.courseId); setForm(c); }}>Sửa</button></article>)}
    </section></div>
  </div></AppShell>;
}

export function ClassesPage() {
  const teacher = getSession()?.role === 'Lecturer';
  const [params] = useSearchParams();
  const [courseId, setCourse] = useState(params.get('courseId') || '');
  const courses = useApi('/api/Courses');
  const classes = useApi(`/api/classes${courseId ? `?courseId=${courseId}` : ''}`);
  const [form, setForm] = useState({ classCode: '', className: '' });
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const save = async e => { e.preventDefault(); setBusy(true); setError(''); try { await api.post('/api/lecturer/classes', { ...form, courseId: Number(courseId) }); setForm({ classCode: '', className: '' }); setNotice('Đã tạo lớp học phần. Mở lớp để ghi danh sinh viên.'); classes.reload(); } catch (err) { setError(err.message); } finally { setBusy(false); } };
  return <AppShell title={teacher ? 'Lớp học phần' : 'Lớp của tôi'} subtitle={teacher ? 'Quản lý danh sách sinh viên theo từng lớp, không gộp theo môn' : 'Chỉ hiển thị lớp bạn đã ghi danh'}><div className="wf-page">
    {(error || classes.error || courses.error) && <p className="wf-error" role="alert">{error || classes.error || courses.error}</p>}{notice && <p className="wf-notice" role="status">{notice}</p>}
    <section className="wf-panel"><label>Lọc/chọn môn học<select value={courseId} onChange={e => setCourse(e.target.value)}><option value="">Tất cả môn</option>{courses.data?.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}</select></label>
      {teacher && <form className="lc-inline-form" onSubmit={save}><label>Mã lớp<input required maxLength={50} value={form.classCode} onChange={e => setForm({ ...form, classCode: e.target.value })} /></label><label>Tên lớp<input required maxLength={200} value={form.className} onChange={e => setForm({ ...form, className: e.target.value })} /></label><button className="wf-button" disabled={busy || !courseId}>{busy ? 'Đang lưu…' : '+ Tạo lớp cho môn đã chọn'}</button></form>}
    </section>
    {classes.loading && <p>Đang tải lớp…</p>}{classes.data?.length === 0 && <p className="wf-empty">Chưa có lớp phù hợp. {teacher && 'Chọn môn rồi tạo lớp học phần.'}</p>}
    <div className="wf-cards lc-gap">{classes.data?.map(c => <article className="wf-card" key={c.classId}><span className="wf-eyebrow">{c.courseCode}</span><h2>{c.classCode} · {c.className}</h2><p>{c.courseName}{teacher && ` · ${c.studentCount} sinh viên`}</p><Link className="wf-button wf-secondary" to={teacher ? `/classes/${c.classId}` : '/exams'}>{teacher ? 'Quản lý lớp & sinh viên' : 'Xem lịch thi'} →</Link></article>)}</div>
  </div></AppShell>;
}

export function ClassWorkspace() {
  const { classId } = useParams(); const classes = useApi('/api/classes');
  const roster = useApi(`/api/lecturer/classes/${classId}/students`); const exams = useApi(`/api/Exams?classId=${classId}`);
  const cl = classes.data?.find(c => c.classId === Number(classId));
  const [term, setTerm] = useState(''); const [results, setResults] = useState([]); const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const [csv, setCsv] = useState('email\n'); const [csvErrors, setCsvErrors] = useState([]); const [edit, setEdit] = useState(null);
  const run = async fn => { setBusy(true); setError(''); try { await fn(); } catch (e) { setError(e.message); } finally { setBusy(false); } };
  const search = (e, next = 1) => { e.preventDefault(); run(async () => { setResults(await api.get(`/api/lecturer/students/search?q=${encodeURIComponent(term.trim())}&page=${next}`)); setPage(next); }); };
  const enroll = id => run(async () => { await api.post(`/api/lecturer/classes/${classId}/students/${id}`); setNotice('Đã ghi danh sinh viên.'); roster.reload(); });
  const remove = id => { if (window.confirm('Gỡ sinh viên khỏi lớp? Lịch thi đang tồn tại sẽ được kiểm tra trước khi gỡ.')) run(async () => { await api.delete(`/api/lecturer/classes/${classId}/students/${id}`); setNotice('Đã gỡ ghi danh, giữ nguyên lịch sử thi.'); roster.reload(); }); };
  const importCsv = e => { e.preventDefault(); run(async () => { const result = await api.post(`/api/lecturer/classes/${classId}/students/import`, { csv }); setCsvErrors(result.errors); setNotice(result.errors.length ? 'CSV có lỗi; chưa thay đổi danh sách lớp.' : `Đã ghi danh ${result.added} sinh viên; ${result.alreadyEnrolled} đã có trong lớp.`); if (!result.errors.length) roster.reload(); }); };
  return <AppShell title={cl ? `${cl.classCode} · ${cl.className}` : 'Quản lý lớp học phần'} subtitle={cl?.courseName}><div className="wf-page">
    <Link className="wf-back" to="/classes">← Danh sách lớp</Link>
    {(error || roster.error || classes.error || exams.error) && <p className="wf-error" role="alert">{error || roster.error || classes.error || exams.error}</p>}{notice && <p className="wf-notice" role="status">{notice}</p>}
    {classes.loading && <p>Đang tải lớp…</p>}{classes.data && !cl && <p className="wf-error">Lớp không tồn tại hoặc bạn không có quyền quản lý.</p>}
    {cl && <>
      <nav className="lc-shortcuts"><Link className="wf-button" to={`/exams/new?courseId=${cl.courseId}&classId=${classId}`}>+ Tạo kỳ thi của lớp</Link><Link className="wf-button wf-secondary" to={`/documents?courseId=${cl.courseId}`}>Tài liệu môn</Link><Link className="wf-button wf-secondary" to={`/questions?courseId=${cl.courseId}`}>Ngân hàng câu hỏi</Link><button className="wf-button wf-secondary" onClick={() => setEdit({ classCode: cl.classCode, className: cl.className })}>Sửa lớp</button></nav>
      {edit && <form className="wf-panel lc-inline-form" onSubmit={e => { e.preventDefault(); run(async () => { await api.put(`/api/lecturer/classes/${classId}`, { ...edit, courseId: cl.courseId }); setEdit(null); classes.reload(); }); }}><label>Mã lớp<input required value={edit.classCode} maxLength={50} onChange={e => setEdit({ ...edit, classCode: e.target.value })} /></label><label>Tên lớp<input required value={edit.className} maxLength={200} onChange={e => setEdit({ ...edit, className: e.target.value })} /></label><button className="wf-button" disabled={busy}>Lưu lớp</button><button className="wf-button wf-secondary" type="button" onClick={() => setEdit(null)}>Hủy</button></form>}
      <div className="lc-columns lc-gap"><section className="wf-panel"><h2>Sinh viên đã ghi danh · {roster.data?.length ?? 0}</h2>{roster.loading && <p>Đang tải…</p>}{roster.data?.length === 0 && <p className="wf-muted">Chưa có sinh viên. Tìm tài khoản có sẵn hoặc nhập CSV bên cạnh.</p>}
        {roster.data?.map(s => <article className="lc-list-row" key={s.studentId}><div><strong>{s.fullName}</strong><small>{s.email}{s.status !== 'Active' && ' · Tài khoản không hoạt động'}</small></div><button className="lc-text-button lc-danger" disabled={busy} onClick={() => remove(s.studentId)}>Gỡ</button></article>)}
      </section><section className="wf-panel"><h2>Ghi danh từ tài khoản có sẵn</h2><form onSubmit={search} className="lc-search"><label>Tên hoặc email sinh viên<input minLength={2} required maxLength={100} value={term} onChange={e => setTerm(e.target.value)} placeholder="Nhập ít nhất 2 ký tự" /></label><button className="wf-button" disabled={busy}>Tìm</button></form>
        <p className="wf-muted">Hệ thống chưa có trường mã sinh viên. Tìm theo tên/email; không tạo tài khoản mới.</p>
        {results.map(s => <article className="lc-list-row" key={s.userId}><div><strong>{s.fullName}</strong><small>{s.email}</small></div><button className="lc-text-button" disabled={busy || roster.data?.some(r => r.studentId === s.userId)} onClick={() => enroll(s.userId)}>{roster.data?.some(r => r.studentId === s.userId) ? 'Đã có' : '+ Ghi danh'}</button></article>)}
        {results.length > 0 && <div className="wf-tools"><button className="wf-button wf-secondary" disabled={page <= 1 || busy} onClick={e => search(e, page - 1)}>Trước</button><span>Trang {page}</span><button className="wf-button wf-secondary" disabled={results.length < 30 || busy} onClick={e => search(e, page + 1)}>Tiếp</button></div>}
        <details className="lc-gap"><summary>Nhập CSV theo email</summary><form onSubmit={importCsv}><p className="wf-muted">Mẫu: cột đầu <code>email</code>, mỗi dòng một email có sẵn. Tối đa 500 dòng/100KB. Nếu có dòng lỗi, toàn bộ danh sách giữ nguyên.</p><input aria-label="Chọn CSV" type="file" accept=".csv,text/csv" onChange={e => { const file = e.target.files?.[0]; if (file?.size > 100000) setError('CSV tối đa 100KB.'); else file?.text().then(setCsv).catch(() => setError('Không đọc được CSV.')); }} /><label>Nội dung CSV<textarea rows={5} value={csv} onChange={e => setCsv(e.target.value)} /></label><button className="wf-button" disabled={busy}>Kiểm tra & ghi danh CSV</button></form>{csvErrors.length > 0 && <ul className="wf-error">{csvErrors.map(line => <li key={line}>{line}</li>)}</ul>}</details>
      </section></div>
      <section className="wf-panel lc-gap"><h2>Kỳ thi của lớp</h2>{exams.loading && <p>Đang tải…</p>}{exams.data?.length === 0 && <p className="wf-muted">Chưa có kỳ thi. Hoàn thiện danh sách sinh viên và ngân hàng câu hỏi trước khi tạo lịch.</p>}{exams.data?.map(e => <article className="lc-list-row" key={e.examId}><div><Link to={`/exams/${e.examId}`}><strong>{e.examName}</strong></Link><small>{vnTime(e.startTime)}</small></div><Status value={e.status} /></article>)}</section>
    </>}
  </div></AppShell>;
}
