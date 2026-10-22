import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import AppShell from '../components/Sidebar.jsx';
import { api } from '../api/client.js';
import { useApi } from '../hooks/useApi.js';
import '../styles/Lecturer.css';

export function DocumentsPage() {
  const [params] = useSearchParams(); const [courseId, setCourse] = useState(params.get('courseId') || '');
  const courses = useApi('/api/Courses'); const files = useApi(courseId ? `/api/documents?courseId=${courseId}` : null);
  const [file, setFile] = useState(null); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const upload = async event => {
    event.preventDefault(); setError(''); setBusy(true);
    try {
      if (!file || file.size > 10 * 1024 * 1024) throw new Error('Chọn PDF/DOCX tối đa 10MB.');
      const body = new FormData(); body.append('file', file); await api.post(`/api/lecturer/courses/${courseId}/documents`, body);
      setNotice('Đã lưu tài liệu riêng tư. Không gửi tài liệu đến dịch vụ AI.'); files.reload();
    } catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  const download = async doc => {
    setError(''); setBusy(true);
    try {
      const blob = await api.get(`/api/documents/${doc.documentId}/download`, { binary: true }); const url = URL.createObjectURL(blob);
      const a = document.createElement('a'); a.href = url; a.download = doc.fileName; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  const archive = async doc => {
    if (!window.confirm(`Gỡ tài liệu “${doc.fileName}” khỏi danh sách? File gốc được giữ riêng, không xóa vĩnh viễn.`)) return;
    setError(''); setBusy(true); try { await api.delete(`/api/lecturer/documents/${doc.documentId}`); files.reload(); setNotice('Đã gỡ tài liệu khỏi danh sách.'); } catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  return <AppShell title="Tài liệu giảng dạy" subtitle="Kho riêng của môn học · PDF/DOCX · tối đa 10MB"><div className="wf-page">
    {(error || courses.error || files.error) && <p className="wf-error" role="alert">{error || courses.error || files.error}</p>}{notice && <p className="wf-notice" role="status">{notice}</p>}
    <section className="wf-panel"><label>Môn học<select value={courseId} onChange={e => setCourse(e.target.value)}><option value="">Chọn môn học</option>{courses.data?.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}</select></label>
      <p className="lc-course-shared">Tài liệu dùng chung cho các lớp của môn do bạn phụ trách. Hệ thống chưa có kho tài liệu riêng từng lớp. Upload này chỉ lưu file và metadata, không xử lý AI.</p>
      {courseId && <form className="lc-file" onSubmit={upload}><input aria-label="Chọn tài liệu PDF hoặc DOCX" type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" required onChange={e => setFile(e.target.files?.[0] || null)} /><button className="wf-button" disabled={busy || !file}>{busy ? 'Đang xử lý…' : 'Tải tài liệu lên'}</button></form>}
    </section>
    {files.loading && <p>Đang tải tài liệu…</p>}{files.data?.length === 0 && <p className="wf-empty">Môn học chưa có tài liệu.</p>}
    {files.data?.length > 0 && <section className="wf-panel lc-gap">{files.data.map(doc => <article className="lc-list-row" key={doc.documentId}><div><strong>{doc.fileName}</strong><small>{(doc.fileSize / 1024).toFixed(1)} KB · Lưu riêng tư</small></div><div className="lc-table-actions"><button className="lc-text-button" disabled={busy} onClick={() => download(doc)}>Tải xuống</button><button className="lc-text-button lc-danger" disabled={busy} onClick={() => archive(doc)}>Gỡ</button></div></article>)}</section>}
  </div></AppShell>;
}

const blank = { questionText: '', expectedAnswer: '', difficulty: 'Easy', bloomLevel: 'Understand', status: 'Draft' };
const levels = { Easy: 'Dễ', Medium: 'Trung bình', Hard: 'Khó' };
const blooms = { Remember: 'Ghi nhớ', Understand: 'Hiểu', Apply: 'Vận dụng', Analyze: 'Phân tích', Evaluate: 'Đánh giá', Create: 'Sáng tạo' };
export function ManualQuestionsPage() {
  const [params] = useSearchParams(); const [courseId, setCourse] = useState(params.get('courseId') || '');
  const courses = useApi('/api/workflow/catalog');
  const [page, setPage] = useState(1); const [search, setSearch] = useState(''); const [term, setTerm] = useState(''); const [status, setStatus] = useState('');
  const questions = useApi(courseId ? `/api/Questions?courseId=${courseId}&page=${page}&pageSize=20&q=${encodeURIComponent(term)}&status=${status}` : null);
  const [form, setForm] = useState(blank); const [editing, setEditing] = useState(null); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const save = async event => {
    event.preventDefault(); setError(''); setBusy(true);
    try { const body = { ...form, courseId: Number(courseId) }; await (editing ? api.put(`/api/lecturer/questions/${editing}`, body) : api.post('/api/lecturer/questions', body)); setNotice('Đã lưu câu hỏi thủ công. Chỉ câu Sẵn sàng mới được dùng để phân đề.'); setEditing(null); setForm(blank); questions.reload(); courses.reload(); }
    catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  const archive = async q => {
    if (!window.confirm('Lưu trữ câu hỏi này? Câu hỏi sẽ không được phân vào đề mới; lịch sử thi vẫn được giữ nguyên.')) return;
    setBusy(true); setError(''); try { await api.delete(`/api/lecturer/questions/${q.questionId}`); questions.reload(); courses.reload(); } catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  const course = courses.data?.find(c => c.courseId === Number(courseId));
  return <AppShell title="Ngân hàng câu hỏi" subtitle="Soạn câu chính thủ công theo môn học · Không tạo câu hỏi bằng AI"><div className="wf-page">
    {(error || courses.error || questions.error) && <p className="wf-error" role="alert">{error || courses.error || questions.error}</p>}{notice && <p className="wf-notice" role="status">{notice}</p>}
    <section className="wf-panel"><label>Môn học<select value={courseId} onChange={e => { setCourse(e.target.value); setPage(1); setEditing(null); setForm(blank); }}><option value="">Chọn môn học</option>{courses.data?.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}</select></label>
      <p className="lc-course-shared">Ngân hàng dùng chung theo môn, phục vụ các lớp bạn phụ trách. {course ? <strong>Hiện có {course.poolSize} câu chính Sẵn sàng.</strong> : 'Chọn môn để xem số câu đủ dùng.'} Chọn câu Nháp để tiếp tục soạn, Sẵn sàng để đưa vào phân đề.</p>
    </section>
    {courseId && <div className="lc-columns lc-gap"><section className="wf-panel"><h2>{editing ? 'Chỉnh sửa câu hỏi' : 'Thêm câu hỏi'}</h2><form onSubmit={save}><fieldset disabled={busy}>
      <label>Nội dung câu hỏi<textarea required rows={5} maxLength={5000} value={form.questionText} onChange={e => setForm({ ...form, questionText: e.target.value })} /></label>
      <label>Đáp án tham khảo / ý cần đạt<textarea rows={4} maxLength={20000} value={form.expectedAnswer || ''} onChange={e => setForm({ ...form, expectedAnswer: e.target.value })} /></label>
      <div className="wf-grid"><label>Độ khó<select value={form.difficulty} onChange={e => setForm({ ...form, difficulty: e.target.value })}>{Object.entries(levels).map(([value, text]) => <option key={value} value={value}>{text}</option>)}</select></label><label>Mức độ nhận thức<select value={form.bloomLevel} onChange={e => setForm({ ...form, bloomLevel: e.target.value })}>{Object.entries(blooms).map(([value, text]) => <option key={value} value={value}>{text}</option>)}</select></label></div>
      <label>Trạng thái<select value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}><option value="Draft">Nháp</option><option value="Approved">Sẵn sàng sử dụng</option></select></label>
      <div className="wf-tools"><button className="wf-button">{busy ? 'Đang lưu…' : 'Lưu câu hỏi'}</button>{editing && <button className="wf-button wf-secondary" type="button" onClick={() => { setEditing(null); setForm(blank); }}>Hủy sửa</button>}</div>
    </fieldset></form></section><section className="wf-panel"><h2>Danh sách câu hỏi</h2><form className="lc-toolbar" onSubmit={e => { e.preventDefault(); setTerm(search); setPage(1); }}><label>Tìm nội dung<input value={search} onChange={e => setSearch(e.target.value)} /></label><label>Trạng thái<select value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Đang sử dụng</option><option value="Draft">Nháp</option><option value="Approved">Sẵn sàng</option><option value="Archived">Đã lưu trữ</option></select></label><button className="wf-button wf-secondary">Tìm</button></form>
      {questions.loading && <p>Đang tải…</p>}{questions.data?.length === 0 && <p className="wf-muted">Chưa có câu hỏi phù hợp.</p>}
      {questions.data?.map(q => <article className="wf-history" key={q.questionId}><p className="wf-muted">{levels[q.difficulty]} · {blooms[q.bloomLevel]} · {q.status === 'Approved' ? 'Sẵn sàng' : q.status === 'Draft' ? 'Nháp' : q.status === 'Archived' ? 'Đã lưu trữ' : 'Chờ duyệt'}{q.source !== 'Manual' && ' · Nguồn có sẵn'}</p><strong className="lc-question">{q.questionText}</strong><details><summary>Đáp án tham khảo</summary><p className="lc-question">{q.expectedAnswer || 'Chưa nhập đáp án tham khảo.'}</p></details><div className="wf-tools">{q.source === 'Manual' && q.questionType === 'Main' && q.status !== 'Archived' && <button className="lc-text-button" disabled={busy} onClick={() => { setEditing(q.questionId); setForm(q); }}>Sửa</button>}{q.status !== 'Archived' && <button className="lc-text-button lc-danger" disabled={busy} onClick={() => archive(q)}>Lưu trữ</button>}</div></article>)}
      <div className="lc-pagination"><button className="wf-button wf-secondary" disabled={page === 1 || questions.loading} onClick={() => setPage(p => p - 1)}>Trước</button><span>Trang {page}</span><button className="wf-button wf-secondary" disabled={questions.data?.length !== 20 || questions.loading} onClick={() => setPage(p => p + 1)}>Tiếp</button></div>
      <Link className="wf-back" to={`/classes?courseId=${courseId}`}>Chọn lớp để tạo lịch thi →</Link>
    </section></div>}
  </div></AppShell>;
}
