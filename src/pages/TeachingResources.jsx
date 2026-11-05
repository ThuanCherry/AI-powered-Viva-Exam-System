import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import AppShell from '../components/Sidebar.jsx';
import { api } from '../api/client.js';
import { useApi } from '../hooks/useApi.js';
import '../styles/Lecturer.css';

export function DocumentsPage() {
  const [params] = useSearchParams();
  const [courseId, setCourse] = useState(params.get('courseId') || '');
  const courses = useApi('/api/Courses');
  const files = useApi(courseId ? `/api/documents?courseId=${courseId}` : null);
  const [file, setFile] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  // AI Question Generation state
  const [selectedDocForAi, setSelectedDocForAi] = useState(null);
  const [genCount, setGenCount] = useState(5);
  const [genBusy, setGenBusy] = useState(false);
  const [genResult, setGenResult] = useState(null);
  const [elapsed, setElapsed] = useState(0);

  // Live timer while AI is generating questions
  useEffect(() => {
    if (!genBusy) return;
    const timer = setInterval(() => {
      setElapsed(s => s + 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [genBusy]);

  const upload = async event => {
    event.preventDefault();
    setError('');
    setBusy(true);
    setNotice('');
    try {
      if (!file || file.size > 10 * 1024 * 1024) throw new Error('Chọn PDF/DOCX tối đa 10MB.');
      const body = new FormData();
      body.append('file', file);
      await api.post(`/api/lecturer/courses/${courseId}/documents`, body);
      setNotice('Đã tải lên tài liệu thành công. Bạn có thể bấm "✨ Sinh câu hỏi AI" để trích xuất và tạo câu hỏi vấn đáp.');
      files.reload();
      setFile(null);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const download = async doc => {
    setError('');
    setBusy(true);
    try {
      const blob = await api.get(`/api/documents/${doc.documentId}/download`, { binary: true });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = doc.fileName;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const archive = async doc => {
    if (!window.confirm(`Gỡ tài liệu “${doc.fileName}” khỏi danh sách? File gốc được giữ riêng, không xóa vĩnh viễn.`)) return;
    setError('');
    setBusy(true);
    try {
      await api.delete(`/api/lecturer/documents/${doc.documentId}`);
      files.reload();
      if (selectedDocForAi?.documentId === doc.documentId) setSelectedDocForAi(null);
      setNotice('Đã gỡ tài liệu khỏi danh sách.');
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const handleStartAiGen = doc => {
    setError('');
    setGenResult(null);
    setSelectedDocForAi(doc);
  };

  const handleGenerateQuestions = async e => {
    e.preventDefault();
    if (!selectedDocForAi) return;
    setError('');
    setGenBusy(true);
    setElapsed(0);
    setGenResult(null);
    try {
      const res = await api.post(`/api/lecturer/documents/${selectedDocForAi.documentId}/generate-questions`, {
        count: Number(genCount)
      });
      setGenResult(res);
      setNotice(`AI đã hoàn thành! Đã sinh ${res.generatedCount} câu hỏi ở trạng thái Nháp và thêm vào Ngân hàng câu hỏi để bạn kiểm duyệt.`);
      files.reload();
    } catch (err) {
      setError(err.message || 'Không thể sinh câu hỏi từ tài liệu.');
    } finally {
      setGenBusy(false);
    }
  };

  return (
    <AppShell title="Tài liệu & Sinh câu hỏi AI" subtitle="Đọc tài liệu PDF/DOCX (RAG) · AI tự sinh câu hỏi vấn đáp">
      <div className="wf-page">
        {(error || courses.error || files.error) && <p className="wf-error" role="alert">{error || courses.error || files.error}</p>}
        {notice && <p className="wf-notice" role="status">{notice}</p>}

        <section className="wf-panel">
          <label>Môn học
            <select value={courseId} onChange={e => { setCourse(e.target.value); setSelectedDocForAi(null); setGenResult(null); }}>
              <option value="">Chọn môn học</option>
              {courses.data?.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}
            </select>
          </label>
          <p className="lc-course-shared">
            Tài liệu dùng chung cho môn học. Hệ thống hỗ trợ tài liệu giáo trình, slide hoặc bài giảng (PDF / DOCX tối đa 10MB).
            Bạn có thể yêu cầu AI đọc và phân tích nội dung để tự động trích xuất các câu hỏi thi vấn đáp trọng tâm.
          </p>
          {courseId && (
            <form className="lc-file" onSubmit={upload}>
              <input aria-label="Chọn tài liệu PDF hoặc DOCX" type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" required onChange={e => setFile(e.target.files?.[0] || null)} />
              <button className="wf-button" disabled={busy || !file}>{busy ? 'Đang lưu…' : 'Tải tài liệu lên'}</button>
            </form>
          )}
        </section>

        {/* Modal / Panel for AI Question Generation */}
        {selectedDocForAi && (
          <section className="wf-panel" style={{ border: '2px solid #10b981', background: '#f0fdf4', marginTop: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <h2 style={{ margin: '0 0 6px 0', color: '#065f46' }}>✨ Sinh câu hỏi vấn đáp bằng AI</h2>
                <p style={{ margin: 0, fontSize: '14px', color: '#047857' }}>
                  Tài liệu: <strong>{selectedDocForAi.fileName}</strong> ({(selectedDocForAi.fileSize / 1024).toFixed(1)} KB)
                </p>
              </div>
              <button className="lc-text-button" type="button" onClick={() => setSelectedDocForAi(null)}>✕ Đóng</button>
            </div>

            <form onSubmit={handleGenerateQuestions} style={{ marginTop: '16px' }}>
              <div className="wf-grid" style={{ alignItems: 'end' }}>
                <label>Số lượng câu hỏi muốn sinh
                  <select value={genCount} onChange={e => setGenCount(e.target.value)} disabled={genBusy}>
                    <option value={3}>3 câu hỏi trọng tâm</option>
                    <option value={5}>5 câu hỏi (Khuyến nghị)</option>
                    <option value={8}>8 câu hỏi</option>
                    <option value={10}>10 câu hỏi bao quát</option>
                  </select>
                </label>
                <div>
                  <button className="wf-button" style={{ background: '#059669', borderColor: '#047857' }} disabled={genBusy}>
                    {genBusy ? '🤖 AI đang đọc tài liệu & sinh câu hỏi…' : '🚀 Bắt đầu sinh câu hỏi bằng AI'}
                  </button>
                </div>
              </div>
              {genBusy && (
                <div style={{ marginTop: '14px', padding: '14px', background: '#ecfdf5', borderRadius: '8px', border: '1px solid #a7f3d0' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <span style={{ fontSize: '16px' }}>🤖</span>
                    <strong style={{ fontSize: '14px', color: '#065f46' }}>
                      AI đang phân tích tài liệu và thiết kế câu hỏi vấn đáp... ({elapsed}s)
                    </strong>
                  </div>
                  <p style={{ margin: '6px 0 0 0', fontSize: '13px', color: '#047857' }}>
                    Gemini đang đọc toàn bộ nội dung tài liệu, trích xuất các ý chính, cấu trúc đáp án chuẩn và gán mức độ nhận thức (Bloom Level). Quá trình này thường mất khoảng 10 – 20 giây. Bạn có thể chuyển tab làm việc khác và quay lại mà không bị mất tiến trình.
                  </p>
                </div>
              )}
            </form>

            {/* Preview of Generated Questions */}
            {genResult && (
              <div style={{ marginTop: '20px', borderTop: '1px solid #a7f3d0', paddingTop: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h3 style={{ margin: 0, color: '#065f46' }}>
                    ✅ Đã tạo {genResult.generatedCount} câu hỏi (Trạng thái: Nháp):
                  </h3>
                  <Link className="wf-button wf-secondary" to={`/questions?courseId=${courseId}`}>
                    Kiểm duyệt trong Ngân hàng câu hỏi →
                  </Link>
                </div>
                <div style={{ marginTop: '12px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  {genResult.questions?.map((q, idx) => (
                    <div key={idx} style={{ background: 'white', padding: '12px', borderRadius: '8px', border: '1px solid #d1fae5' }}>
                      <p style={{ margin: '0 0 6px 0', fontSize: '12px', color: '#d97706', fontWeight: 600 }}>
                        Câu {idx + 1} · {q.difficulty} · {q.bloomLevel} · [Nháp - Chờ duyệt]
                      </p>
                      <strong style={{ fontSize: '14px', color: '#1f2937' }}>{q.questionText}</strong>
                      {q.expectedAnswer && (
                        <p style={{ margin: '6px 0 0 0', fontSize: '13px', color: '#4b5563' }}>
                          <em>Đáp án chuẩn:</em> {q.expectedAnswer}
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
        )}

        {files.loading && !files.data && <p>Đang tải tài liệu…</p>}
        {files.data?.length === 0 && <p className="wf-empty">Môn học chưa có tài liệu.</p>}
        {files.data?.length > 0 && (
          <section className="wf-panel lc-gap">
            <h2>Danh sách tài liệu học phần ({files.data.length})</h2>
            {files.data.map(doc => (
              <article className="lc-list-row" key={doc.documentId}>
                <div>
                  <strong>{doc.fileName}</strong>
                  <small>{(doc.fileSize / 1024).toFixed(1)} KB · {doc.status === 'Processed' ? 'Đã lập chỉ mục AI' : 'Tài liệu gốc'}</small>
                </div>
                <div className="lc-table-actions">
                  <button className="lc-text-button" disabled={busy || genBusy} onClick={() => download(doc)}>Tải xuống</button>
                  <button
                    className="wf-button wf-secondary"
                    style={{ padding: '4px 12px', fontSize: '13px', fontWeight: 600, color: '#065f46', borderColor: '#10b981' }}
                    disabled={busy || genBusy}
                    onClick={() => handleStartAiGen(doc)}
                  >
                    ✨ Sinh câu hỏi AI
                  </button>
                  <button className="lc-text-button lc-danger" disabled={busy || genBusy} onClick={() => archive(doc)}>Gỡ</button>
                </div>
              </article>
            ))}
          </section>
        )}
      </div>
    </AppShell>
  );
}

const blank = { questionText: '', expectedAnswer: '', difficulty: 'Easy', bloomLevel: 'Understand', status: 'Draft' };
const levels = { Easy: 'Dễ', Medium: 'Trung bình', Hard: 'Khó' };
const blooms = { Remember: 'Ghi nhớ', Understand: 'Hiểu', Apply: 'Vận dụng', Analyze: 'Phân tích', Evaluate: 'Đánh giá', Create: 'Sáng tạo' };

export function ManualQuestionsPage() {
  const [params] = useSearchParams();
  const [courseId, setCourse] = useState(params.get('courseId') || '');
  const courses = useApi('/api/workflow/catalog');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [status, setStatus] = useState('');
  const questions = useApi(courseId ? `/api/Questions?courseId=${courseId}&page=${page}&pageSize=20&q=${encodeURIComponent(term)}&status=${status}` : null);
  const [form, setForm] = useState(blank);
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const save = async event => {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      const body = { ...form, courseId: Number(courseId) };
      await (editing ? api.put(`/api/lecturer/questions/${editing}`, body) : api.post('/api/lecturer/questions', body));
      setNotice('Đã lưu câu hỏi thành công. Câu hỏi Sẵn sàng sẽ được dùng để phân đề thi.');
      setEditing(null);
      setForm(blank);
      questions.reload();
      courses.reload();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const archive = async q => {
    if (!window.confirm('Lưu trữ câu hỏi này? Câu hỏi sẽ không được phân vào đề mới; lịch sử thi vẫn được giữ nguyên.')) return;
    setBusy(true);
    setError('');
    try {
      await api.delete(`/api/lecturer/questions/${q.questionId}`);
      questions.reload();
      courses.reload();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const approve = async q => {
    setBusy(true);
    setError('');
    try {
      await api.put(`/api/lecturer/questions/${q.questionId}`, {
        courseId: Number(courseId),
        questionText: q.questionText,
        expectedAnswer: q.expectedAnswer || '',
        difficulty: q.difficulty,
        bloomLevel: q.bloomLevel,
        status: 'Approved'
      });
      setNotice('Đã duyệt câu hỏi chuyển sang trạng thái Sẵn sàng sử dụng.');
      questions.reload();
      courses.reload();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const course = courses.data?.find(c => c.courseId === Number(courseId));

  return (
    <AppShell title="Ngân hàng câu hỏi" subtitle="Soạn câu chính thủ công hoặc tự động sinh từ tài liệu bằng AI">
      <div className="wf-page">
        {(error || courses.error || questions.error) && <p className="wf-error" role="alert">{error || courses.error || questions.error}</p>}
        {notice && <p className="wf-notice" role="status">{notice}</p>}

        <section className="wf-panel">
          <label>Môn học
            <select value={courseId} onChange={e => { setCourse(e.target.value); setPage(1); setEditing(null); setForm(blank); }}>
              <option value="">Chọn môn học</option>
              {courses.data?.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}
            </select>
          </label>
          <p className="lc-course-shared">
            Ngân hàng dùng chung theo môn. {course ? <strong>Hiện có {course.poolSize} câu chính Sẵn sàng.</strong> : 'Chọn môn để xem số câu đủ dùng.'}{' '}
            Bạn có thể soạn trực tiếp tại đây hoặc tải giáo trình lên ở mục{' '}
            <Link to={courseId ? `/documents?courseId=${courseId}` : '/documents'} style={{ fontWeight: 600, color: '#047857' }}>
              Tài liệu & Sinh câu hỏi AI →
            </Link>
          </p>
        </section>

        {courseId && (
          <div className="lc-columns lc-gap">
            <section className="wf-panel">
              <h2>{editing ? 'Chỉnh sửa câu hỏi' : 'Thêm câu hỏi thủ công'}</h2>
              <form onSubmit={save}>
                <fieldset disabled={busy}>
                  <label>Nội dung câu hỏi
                    <textarea required rows={5} maxLength={5000} value={form.questionText} onChange={e => setForm({ ...form, questionText: e.target.value })} />
                  </label>
                  <label>Đáp án tham khảo / ý cần đạt
                    <textarea rows={4} maxLength={20000} value={form.expectedAnswer || ''} onChange={e => setForm({ ...form, expectedAnswer: e.target.value })} />
                  </label>
                  <div className="wf-grid">
                    <label>Độ khó
                      <select value={form.difficulty} onChange={e => setForm({ ...form, difficulty: e.target.value })}>
                        {Object.entries(levels).map(([value, text]) => <option key={value} value={value}>{text}</option>)}
                      </select>
                    </label>
                    <label>Mức độ nhận thức
                      <select value={form.bloomLevel} onChange={e => setForm({ ...form, bloomLevel: e.target.value })}>
                        {Object.entries(blooms).map(([value, text]) => <option key={value} value={value}>{text}</option>)}
                      </select>
                    </label>
                  </div>
                  <label>Trạng thái
                    <select value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}>
                      <option value="Draft">Nháp</option>
                      <option value="Approved">Sẵn sàng sử dụng</option>
                    </select>
                  </label>
                  <div className="wf-tools">
                    <button className="wf-button">{busy ? 'Đang lưu…' : 'Lưu câu hỏi'}</button>
                    {editing && (
                      <button className="wf-button wf-secondary" type="button" onClick={() => { setEditing(null); setForm(blank); }}>
                        Hủy sửa
                      </button>
                    )}
                  </div>
                </fieldset>
              </form>
            </section>

            <section className="wf-panel">
              <h2>Danh sách câu hỏi</h2>
              <form className="lc-toolbar" onSubmit={e => { e.preventDefault(); setTerm(search); setPage(1); }}>
                <label>Tìm nội dung<input value={search} onChange={e => setSearch(e.target.value)} /></label>
                <label>Trạng thái
                  <select value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}>
                    <option value="">Đang sử dụng</option>
                    <option value="Draft">Nháp</option>
                    <option value="Approved">Sẵn sàng</option>
                    <option value="Archived">Đã lưu trữ</option>
                  </select>
                </label>
                <button className="wf-button wf-secondary">Tìm</button>
              </form>

              {questions.loading && !questions.data && <p>Đang tải…</p>}
              {questions.data?.length === 0 && <p className="wf-muted">Chưa có câu hỏi phù hợp.</p>}
              {questions.data?.map(q => (
                <article className="wf-history" key={q.questionId}>
                  <p className="wf-muted">
                    {levels[q.difficulty] || q.difficulty} · {blooms[q.bloomLevel] || q.bloomLevel} · {q.status === 'Approved' ? 'Sẵn sàng' : q.status === 'Draft' ? 'Nháp' : q.status === 'Archived' ? 'Đã lưu trữ' : 'Chờ duyệt'}
                    {q.source === 'AI' ? <span style={{ color: '#059669', fontWeight: 600 }}> · 🤖 Sinh bởi AI</span> : q.source !== 'Manual' ? ' · Nguồn có sẵn' : ' · Soạn thủ công'}
                  </p>
                  <strong className="lc-question">{q.questionText}</strong>
                  <details>
                    <summary>Đáp án tham khảo</summary>
                    <p className="lc-question">{q.expectedAnswer || 'Chưa nhập đáp án tham khảo.'}</p>
                  </details>
                  <div className="wf-tools">
                    {q.status === 'Draft' && (q.source === 'Manual' || q.source === 'AI') && q.questionType === 'Main' && (
                      <button className="lc-text-button" style={{ color: '#059669', fontWeight: 600 }} disabled={busy} onClick={() => approve(q)}>
                        ✓ Duyệt sẵn sàng
                      </button>
                    )}
                    {(q.source === 'Manual' || q.source === 'AI') && q.questionType === 'Main' && q.status !== 'Archived' && (
                      <button className="lc-text-button" disabled={busy} onClick={() => { setEditing(q.questionId); setForm(q); }}>
                        Sửa
                      </button>
                    )}
                    {q.status !== 'Archived' && (
                      <button className="lc-text-button lc-danger" disabled={busy} onClick={() => archive(q)}>
                        Lưu trữ
                      </button>
                    )}
                  </div>
                </article>
              ))}

              <div className="lc-pagination">
                <button className="wf-button wf-secondary" disabled={page === 1 || questions.loading} onClick={() => setPage(p => p - 1)}>Trước</button>
                <span>Trang {page}</span>
                <button className="wf-button wf-secondary" disabled={questions.data?.length !== 20 || questions.loading} onClick={() => setPage(p => p + 1)}>Tiếp</button>
              </div>
              <Link className="wf-back" to={`/classes?courseId=${courseId}`}>Chọn lớp để tạo lịch thi →</Link>
            </section>
          </div>
        )}
      </div>
    </AppShell>
  );
}
