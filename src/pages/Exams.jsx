import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import AppShell from '../components/Sidebar.jsx';
import { api } from '../api/client.js';
import { getSession } from '../auth/session.js';
import '../styles/Workflow.css';
import { vnTime } from '../lib/time.js';
import { useApi } from '../hooks/useApi.js';
import ExamDateTimePicker from '../components/ExamDateTimePicker.jsx';
import { buildExamSchedule, suggestExamStart } from '../lib/examSchedule.js';
import { examSaveIssues } from '../lib/examValidation.js';
import { SCHEDULE_CHANGED } from '../hooks/useScheduleRealtime.js';

const labels = { Draft: 'Bản nháp', Scheduled: 'Đã công bố', InProgress: 'Đang thi', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', Waiting: 'Chờ thi', Absent: 'Vắng thi' };
export function Status({ value }) { return <span className={`wf-status wf-status-${value}`}>{labels[value] || value}</span>; }

export default function Exams() {
  const lecturer = getSession()?.role === 'Lecturer';
  const { data: rows, error } = useApi(lecturer ? '/api/Exams' : '/api/schedule/mine');
  return <AppShell title={lecturer ? 'Quản lý kỳ thi' : 'Lịch thi của tôi'} subtitle="Lịch hiển thị theo giờ Việt Nam · Asia/Ho_Chi_Minh" actions={lecturer && <Link className="wf-button" to="/exams/new">+ Tạo kỳ thi</Link>}>
    <div className="wf-page">
      {error && <p className="wf-error" role="alert">{error}</p>}
      {!rows && !error && <p role="status">Đang tải lịch thi…</p>}
      {rows?.length === 0 && <div className="wf-empty"><h2>{lecturer ? 'Chưa có kỳ thi' : 'Chưa có lịch được công bố'}</h2><p>{lecturer ? 'Tạo bản nháp, kiểm tra slot rồi công bố cho sinh viên.' : 'Lịch được giảng viên công bố sẽ xuất hiện tại đây.'}</p></div>}
      <div className="wf-cards">{rows?.map(e => <article className="wf-card" key={e.examId}>
        <div className="wf-row"><span className="wf-eyebrow">{e.courseName}</span><Status value={e.status} /></div>
        <h2>{e.examName}</h2><p>{vnTime(e.scheduledTime || e.startTime)}</p>
        <p className="wf-muted">{lecturer ? `Kết thúc: ${vnTime(e.endTime)}` : `${e.timePerStudent} phút / sinh viên`}</p>
        <Link className="wf-button wf-secondary" to={lecturer ? `/exams/${e.examId}` : `/viva/${e.examId}`}>{lecturer ? 'Xem lịch & cấu hình' : e.status === 'Completed' ? 'Xem phiên thi' : 'Vào phòng chờ'} →</Link>
      </article>)}</div>
    </div>
  </AppShell>;
}

const empty = { courseId: '', classId: '', examName: '', description: '', startTime: '', timePerStudent: 15, mainQuestionCount: 3, maxFollowUpCount: 2, maxFollowUpsPerQuestion: 1, secondsPerQuestion: 120, strategy: 'Random', studentIds: [] };
function localInput(value) { return value ? new Date(new Date(value).getTime() + 7 * 3600000).toISOString().slice(0, 16) : ''; }

export function ExamEditor() {
  const { examId } = useParams();
  const [params] = useSearchParams();
  const presetCourse = params.get('courseId') || '';
  const presetClass = params.get('classId') || '';
  const navigate = useNavigate();
  const [form, setForm] = useState(empty);
  const [catalog, setCatalog] = useState([]);
  const [students, setStudents] = useState([]);
  const [exam, setExam] = useState(null);
  const [ready, setReady] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [saveError, setSaveError] = useState('');
  const [confirmPublished, setConfirmPublished] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [now, setNow] = useState(() => Date.now());
  const classes = useApi(form.courseId ? `/api/classes?courseId=${form.courseId}` : null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const updated = event => { if (!dirty && !busy && (!event.detail?.examId || event.detail.examId === Number(examId))) setRevision(r => r + 1); };
    window.addEventListener(SCHEDULE_CHANGED, updated);
    return () => window.removeEventListener(SCHEDULE_CHANGED, updated);
  }, [examId, dirty, busy]);
  useEffect(() => {
    if (!exam || !['Draft', 'Scheduled'].includes(exam.status)) return;
    const remaining = new Date(exam.startTime).getTime() - now;
    if (remaining <= 0) return;
    const timer = setTimeout(() => setNow(Date.now()), Math.min(remaining + 10, 2147483647));
    return () => clearTimeout(timer);
  }, [exam, now]);
  const published = exam?.status === 'Scheduled';
  const editable = !exam || (['Draft', 'Scheduled'].includes(exam.status) && new Date(exam.startTime).getTime() > now && exam.candidates.every(c => !c.startedAt && !['InProgress', 'Completed'].includes(c.status)));
  const calculated = buildExamSchedule(form.startTime, form.timePerStudent, form.studentIds);
  const endTime = !editable && exam ? exam.endTime : calculated.endTime;
  const slots = !editable && exam ? exam.candidates.map(c => ({ studentId: c.studentId, startTime: c.scheduledTime, endTime: c.scheduledEndTime || (c.scheduledTime ? new Date(new Date(c.scheduledTime).getTime() + exam.timePerStudent * 60000).toISOString() : null) })) : calculated.slots;
  useEffect(() => {
    const c = new AbortController();
    Promise.all([api.get('/api/workflow/catalog', { signal: c.signal }), examId ? api.get(`/api/Exams/${examId}`, { signal: c.signal }) : null]).then(([courses, e]) => {
      setCatalog(courses); setExam(e);
      setForm(e ? { ...empty, ...e, classId: e.classId || '', startTime: localInput(e.startTime), ...e.vivaSettings, studentIds: e.candidates.map(s => s.studentId) } : { ...empty, courseId: presetCourse, classId: presetClass, startTime: suggestExamStart() });
      setReady(true);
    }).catch(e => { if (e.name !== 'AbortError') setError(e.message); });
    return () => c.abort();
  }, [examId, presetCourse, presetClass, revision]);
  useEffect(() => {
    if (!form.courseId || (!form.classId && (!exam || exam.classId != null))) return;
    const c = new AbortController();
    api.get(form.classId ? `/api/lecturer/classes/${form.classId}/students` : `/api/courses/${form.courseId}/students`, { signal: c.signal }).then(rows => setStudents(rows.filter(s => !s.status || s.status === 'Active'))).catch(e => { if (e.name !== 'AbortError') setError(e.message); });
    return () => c.abort();
  }, [form.courseId, form.classId, exam]);
  const poolSize = catalog.find(c => c.courseId === Number(form.courseId))?.poolSize;
  const saveIssues = examSaveIssues(form, poolSize, !exam || exam.classId != null);
  const field = (key, value) => { setDirty(true); setSaveError(''); setConfirmPublished(false); setForm(f => ({ ...f, [key]: value })); };
  const save = async event => {
    event.preventDefault();
    if (busy) return;
    setError(''); setSaveError('');
    if (saveIssues.length) { setSaveError(saveIssues.join(' ')); return; }
    if (published && !confirmPublished) { setConfirmPublished(true); return; }
    setBusy(true);
    try {
      if (!editable) throw new Error('Lịch đã bắt đầu hoặc đã kết thúc, không thể chỉnh sửa.');
      if (!calculated.endTime) throw new Error('Chọn ngày/giờ bắt đầu, thời lượng hợp lệ và ít nhất một sinh viên.');
      const body = { ...form, endTime: calculated.endTime, courseId: Number(form.courseId), classId: Number(form.classId) || null, startTime: `${form.startTime}:00+07:00` };
      const options = { signal: AbortSignal.timeout(20000) };
      const result = examId ? await api.put(`/api/Exams/${examId}`, body, options) : await api.post('/api/Exams', body, options);
      setDirty(false);
      if (!examId) navigate(`/exams/${result.examId}`);
      else { setExam(await api.get(`/api/Exams/${examId}`, options)); setNotice(published ? 'Đã cập nhật lịch đã công bố. Sinh viên được đồng bộ qua SignalR.' : 'Đã lưu bản nháp. Kiểm tra lịch bên dưới trước khi công bố.'); }
    } catch (e) { setSaveError(e.name === 'TimeoutError' || e.name === 'AbortError' ? 'Máy chủ phản hồi quá 20 giây. Kiểm tra danh sách kỳ thi trước khi thử lưu lại để tránh tạo trùng.' : e.message); } finally { setBusy(false); setConfirmPublished(false); }
  };
  const action = async name => {
    if (!window.confirm(name === 'publish' ? 'Công bố lịch đã lưu và phân câu cố định cho sinh viên?' : 'Hủy kỳ thi này? Sinh viên sẽ không thể bắt đầu thi.')) return;
    setError(''); setBusy(true);
    try { const r = await api.post(`/api/Exams/${examId}/${name}`); setNotice(r.warnings?.join(' ') || (name === 'publish' ? 'Đã công bố lịch và lưu phân câu.' : 'Đã hủy kỳ thi.')); setExam(await api.get(`/api/Exams/${examId}`)); }
    catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  return <AppShell title={examId ? 'Chi tiết kỳ thi' : 'Tạo kỳ thi'} subtitle="01 Cấu hình → 02 Xem trước lịch → 03 Công bố"><div className="wf-page">
    <Link to="/exams" className="wf-back">← Danh sách kỳ thi</Link>
    {(error || classes.error) && <p className="wf-error" role="alert">{error || classes.error}</p>}{notice && <p className="wf-notice" role="status">{notice}</p>}
    {!ready ? <p>Đang tải cấu hình…</p> : <form onSubmit={save} className="wf-editor es-editor">
      <section className="wf-panel"><div className="wf-row"><h2>Thông tin & cấu hình</h2>{exam && <Status value={exam.status} />}</div>
        {published && editable && <p className="wf-notice">Bạn có thể sửa thời gian, danh sách sinh viên và giới hạn trả lời. Môn/lớp, số câu chính và chiến lược đã phân được giữ nguyên.</p>}
        <fieldset disabled={!editable || busy}>
          <label>Môn học<select disabled={published} required value={form.courseId} onChange={e => { field('courseId', e.target.value); field('classId', ''); field('studentIds', []); setStudents([]); }}><option value="">Chọn môn học</option>{catalog.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}</select></label>
          <label>Lớp học phần<select disabled={published} required={!exam || exam.classId != null} value={form.classId} onChange={e => { field('classId', e.target.value); field('studentIds', []); setStudents([]); }}><option value="">{exam && !exam.classId ? 'Lịch cũ chưa gắn lớp' : 'Chọn lớp học phần'}</option>{classes.data?.map(c => <option key={c.classId} value={c.classId}>{c.classCode} · {c.className}</option>)}</select></label>
          {form.courseId && !classes.loading && classes.data?.length === 0 && <p className="wf-muted">Chưa có lớp cho môn này. <Link to={`/classes?courseId=${form.courseId}`}>Tạo lớp và ghi danh sinh viên →</Link></p>}
          <label>Tên kỳ thi<input required maxLength={200} value={form.examName} onChange={e => field('examName', e.target.value)} /></label>
          <label>Mô tả<textarea maxLength={1000} value={form.description || ''} onChange={e => field('description', e.target.value)} /></label>
          <ExamDateTimePicker value={form.startTime} onChange={value => field('startTime', value)} disabled={!editable || busy} />
          <div className="es-timing-summary"><label>Phút / sinh viên<input required type="number" min={1} max={180} value={form.timePerStudent} onChange={e => field('timePerStudent', Number(e.target.value))} /><small className="wf-muted">Thi tuần tự, không nghỉ giữa lượt</small></label><div className="es-end"><span>Kết thúc dự kiến</span><output aria-label="Thời gian kết thúc dự kiến" aria-live="polite">{endTime ? vnTime(endTime) : 'Chọn thời gian và sinh viên'}</output></div></div>
          <div className="wf-grid">{[['mainQuestionCount', 'Số câu chính', 1, 20], ['maxFollowUpCount', 'Câu đào sâu tổng / sinh viên', 0, 100], ['maxFollowUpsPerQuestion', 'Câu đào sâu tối đa / câu chính', 0, 5], ['secondsPerQuestion', 'Giây / lượt trả lời', 15, 1800]].map(([key, label, min, max]) => <label key={key}>{label}<input disabled={published && key === 'mainQuestionCount'} required type="number" min={min} max={max} value={form[key]} onChange={e => field(key, Number(e.target.value))} /></label>)}
          <label>Chiến lược<select disabled={published} value={form.strategy} onChange={e => field('strategy', e.target.value)}><option value="Random">Ngẫu nhiên</option><option value="Adaptive">Cân bằng độ khó</option></select></label></div>
          <p className="wf-muted">{poolSize ?? '—'} câu chính Sẵn sàng. {poolSize == null ? 'Chọn môn để kiểm tra ngân hàng. ' : poolSize < form.mainQuestionCount ? 'Chưa đủ câu để tạo kỳ thi. ' : 'Đủ câu theo cấu hình hiện tại. '}<Link to={`/questions?courseId=${form.courseId}`}>Quản lý ngân hàng →</Link> Chiến lược cân bằng độ khó chia câu Dễ / Trung bình / Khó, không gọi AI khi xếp lịch.</p>
          <h3>Sinh viên · {form.studentIds.length} đã chọn</h3>
          <p className="wf-muted">Thứ tự chọn là thứ tự slot thi. Bỏ chọn rồi chọn lại để chuyển xuống cuối.</p>
          {students.length === 0 && <p>Chọn lớp đã ghi danh sinh viên để xếp lịch.</p>}
          {students.length > 0 && <div className="wf-tools"><button type="button" className="wf-button wf-secondary" onClick={() => field('studentIds', students.map(s => s.userId))}>Chọn tất cả</button><button type="button" className="wf-button wf-secondary" onClick={() => field('studentIds', [])}>Bỏ chọn tất cả</button></div>}
          <div className="wf-students">{students.map(s => <label className="wf-check" key={s.userId}><input type="checkbox" checked={form.studentIds.includes(s.userId)} onChange={e => field('studentIds', e.target.checked ? [...form.studentIds, s.userId] : form.studentIds.filter(id => id !== s.userId))} /><span>{s.fullName}<small>{s.email}</small></span></label>)}</div>
          {editable && <div className="es-save-area">
            {confirmPublished && <div className="wf-fallback" role="alert">Sinh viên liên quan sẽ nhận lịch mới ngay sau khi lưu. Kiểm tra bảng xem trước rồi xác nhận. <button type="button" className="lc-text-button" onClick={() => setConfirmPublished(false)}>Hủy thao tác lưu</button></div>}
            {saveIssues.length > 0 && !saveError && <div id="exam-save-hint" className="es-save-hint"><strong>Cần hoàn tất trước khi lưu:</strong><ul>{saveIssues.map(issue => <li key={issue}>{issue}</li>)}</ul></div>}
            {saveError && <p id="exam-save-error" className="wf-error" role="alert">{saveError}</p>}
            <button className="wf-button" disabled={busy} aria-busy={busy} aria-describedby={saveError ? 'exam-save-error' : saveIssues.length ? 'exam-save-hint' : undefined} type="submit">{busy ? 'Đang lưu…' : confirmPublished ? 'Xác nhận lưu & thông báo' : published ? 'Lưu thay đổi lịch thi' : 'Xác nhận lịch & lưu bản nháp'}</button>
          </div>}
        </fieldset>
      </section>
      <aside className="wf-panel wf-preview es-preview"><div className="es-preview-header"><div><span className="wf-eyebrow">Xem trước lịch thi</span><h2>{form.studentIds.length} sinh viên · {form.timePerStudent} phút / người</h2></div><span className="es-sequential">Thi tuần tự · Không nghỉ giữa lượt</span></div>
        <p className="wf-muted">{!editable && exam ? 'Đang hiển thị các khung giờ đã lưu, không tự thay đổi lịch.' : 'Các khung giờ tự cập nhật theo giờ bắt đầu, sinh viên và thời lượng. Ngày hiển thị theo giờ Việt Nam.'}</p>
        {slots.length > 0 ? <div className="es-table-wrap"><table className="es-table"><thead><tr><th>STT</th><th>Sinh viên</th><th>Bắt đầu</th><th>Kết thúc</th></tr></thead><tbody>{slots.map((slot, i) => <tr key={slot.studentId}><td>{i + 1}</td><td>{students.find(s => s.userId === slot.studentId)?.fullName || exam?.candidates.find(c => c.studentId === slot.studentId)?.studentName || `Sinh viên ${slot.studentId}`}</td><td>{vnTime(slot.startTime)}</td><td>{vnTime(slot.endTime)}</td></tr>)}</tbody></table></div> : <p className="wf-muted">Chọn ngày bắt đầu và sinh viên để xem các khung giờ. Lịch qua nửa đêm sẽ hiển thị ngày kế tiếp.</p>}
        <p className="wf-muted">Lưu bản nháp để backend kiểm tra trùng lịch. Công bố sẽ phân câu cố định cho từng sinh viên.</p>
        {dirty && exam?.status === 'Draft' && <p className="wf-fallback">Lưu thay đổi trước khi công bố.</p>}
        {exam?.status === 'Draft' && <button type="button" className="wf-button" disabled={busy || dirty || !editable} onClick={() => action('publish')}>Công bố bản nháp đã lưu</button>}
        {exam && ['Draft', 'Scheduled'].includes(exam.status) && <button type="button" className="wf-button wf-secondary" disabled={busy} onClick={() => action('cancel')}>Hủy kỳ thi</button>}
      </aside>
    </form>}
  </div></AppShell>;
}
