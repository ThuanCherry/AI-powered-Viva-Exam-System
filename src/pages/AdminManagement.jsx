import { useCallback, useEffect, useMemo, useState } from 'react';
import Sidebar from '../components/Sidebar.jsx';
import { getAdminUsers, createAdminUser, updateAdminUser, toggleUserStatus,
  getAdminCourses, createAdminCourse, assignLecturerToCourse, getSpeechSettings, updateSpeechSettings } from '../api/admin.js';
import '../styles/AdminManagement.css';

const roles = { Admin: 'Quản trị viên', Lecturer: 'Giảng viên', Student: 'Sinh viên' };
const emptyForm = { fullName: '', email: '', password: '', role: 'Lecturer' };
const emptyCourse = { courseCode: '', courseName: '', description: '', lecturerId: '' };

const configured = value => value === 'Configured' ? 'Đang hoạt động' : 'Chưa cấu hình';
const fetchAdminData = () => Promise.allSettled([getAdminUsers(), getAdminCourses(), getSpeechSettings()]);

export default function AdminManagement() {
  const [tab, setTab] = useState('users');
  const [users, setUsers] = useState([]);
  const [courses, setCourses] = useState([]);
  const [settings, setSettings] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  
  // User Modal
  const [modal, setModal] = useState(null);
  const [form, setForm] = useState(emptyForm);

  // Course Modal (Admin creates new Course)
  const [modalCourse, setModalCourse] = useState(false);
  const [courseForm, setCourseForm] = useState(emptyCourse);

  const applyData = useCallback(results => {
    const setters = [setUsers, setCourses, setSettings];
    const failures = [];
    results.forEach((result, i) => {
      if (result.status === 'fulfilled') setters[i](result.value);
      else failures.push(result.reason.message);
    });
    setError([...new Set(failures)].join(' '));
    setLoading(false);
  }, []);

  const load = async () => applyData(await fetchAdminData());

  useEffect(() => {
    let alive = true;
    fetchAdminData().then(results => { if (alive) applyData(results); });
    return () => { alive = false; };
  }, [applyData]);

  const lecturers = users.filter(u => u.role === 'Lecturer' && u.status === 'Active');
  
  const filteredUsers = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('vi');
    return users.filter(u => (!roleFilter || u.role === roleFilter)
      && `${u.fullName} ${u.email}`.toLocaleLowerCase('vi').includes(term));
  }, [users, search, roleFilter]);

  const filteredCourses = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('vi');
    return courses.filter(c => `${c.courseCode} ${c.courseName} ${c.lecturerName ?? ''}`
      .toLocaleLowerCase('vi').includes(term));
  }, [courses, search]);

  const mutate = async (action, message) => {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await action();
      await load();
      setNotice(message);
    } catch (err) { setError(err.message); }
    finally { setBusy(false); }
  };

  const openUser = user => {
    setError(''); setNotice(''); setModal(user ?? {});
    setForm(user ? { ...emptyForm, fullName: user.fullName, email: user.email, role: user.role } : { ...emptyForm });
  };

  const saveUser = event => {
    event.preventDefault();
    void mutate(async () => {
      if (modal.userId) await updateAdminUser(modal.userId, { fullName: form.fullName.trim(), role: form.role });
      else await createAdminUser({ ...form, fullName: form.fullName.trim(), email: form.email.trim() });
      setModal(null);
    }, modal.userId ? 'Đã cập nhật tài khoản.' : 'Đã tạo tài khoản. Người dùng có thể đăng nhập.');
  };

  const saveCourse = event => {
    event.preventDefault();
    void mutate(async () => {
      await createAdminCourse({
        courseCode: courseForm.courseCode.trim().toUpperCase(),
        courseName: courseForm.courseName.trim(),
        description: courseForm.description?.trim(),
        lecturerId: courseForm.lecturerId ? Number(courseForm.lecturerId) : (lecturers[0]?.userId ?? null)
      });
      setModalCourse(false);
      setCourseForm(emptyCourse);
    }, 'Đã khởi tạo môn học mới và phân công giảng viên thành công.');
  };

  const toggle = user => {
    const action = user.status === 'Active' ? 'khóa' : 'mở khóa';
    if (window.confirm(`Bạn muốn ${action} tài khoản ${user.email}?`))
      void mutate(() => toggleUserStatus(user.userId), `Đã ${action} tài khoản.`);
  };

  return (
    <Sidebar
      title="Quản trị hệ thống"
      subtitle="Quản lý tài khoản, phân quyền môn học và cấu hình AI STT/TTS."
      actions={
        tab === 'users' ? (
          <button className="admin-btn admin-btn--primary" disabled={busy || loading} onClick={() => openUser(null)}>
            + Tạo tài khoản
          </button>
        ) : tab === 'courses' ? (
          <button className="admin-btn admin-btn--primary" disabled={busy || loading} onClick={() => {
            setError('');
            setCourseForm({ ...emptyCourse, lecturerId: lecturers[0]?.userId ?? '' });
            setModalCourse(true);
          }}>
            + Thêm môn học mới
          </button>
        ) : null
      }
    >
      {error && !modal && !modalCourse && <div className="admin-error" role="alert">{error}</div>}
      {notice && <div className="toast-success" role="status"><span>✓</span> {notice}</div>}

      <div className="admin-tabs" aria-label="Chức năng quản trị">
        {[
          ['users', 'Quản lý tài khoản', users.length],
          ['courses', 'Môn học & Phân quyền', courses.length],
          ['settings', 'Cấu hình AI & Ngôn ngữ', null]
        ].map(([id, label, count]) => (
          <button
            key={id}
            className={`admin-tab-btn ${tab === id ? 'is-active' : ''}`}
            disabled={busy}
            onClick={() => { setTab(id); setSearch(''); setNotice(''); }}
          >
            {label}
            {count !== null && <span className="admin-tab-count">{count}</span>}
          </button>
        ))}
      </div>

      <div className="admin-toolbar">
        {tab !== 'settings' && (
          <div className="admin-filters">
            <input
              className="admin-search"
              aria-label="Tìm kiếm"
              placeholder={tab === 'users' ? 'Tìm tên hoặc email…' : 'Tìm mã môn, tên môn hoặc giảng viên…'}
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
            {tab === 'users' && (
              <select className="admin-select" aria-label="Lọc vai trò" value={roleFilter} onChange={e => setRoleFilter(e.target.value)}>
                <option value="">Tất cả vai trò</option>
                {Object.entries(roles).map(([value, name]) => <option key={value} value={value}>{name}</option>)}
              </select>
            )}
          </div>
        )}
        <button className="admin-btn admin-btn--ghost" disabled={loading || busy} onClick={() => { setLoading(true); void load(); }}>
          {loading ? 'Đang tải…' : '🔄 Tải lại dữ liệu'}
        </button>
      </div>

      {loading && <p role="status">Đang tải dữ liệu từ máy chủ…</p>}

      {/* TAB 1: USERS */}
      {tab === 'users' && (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Người dùng</th>
                <th>Vai trò</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.map(user => (
                <tr key={user.userId}>
                  <td>
                    <div className="user-cell">
                      <div className="user-avatar">{user.fullName?.charAt(0)?.toUpperCase() || 'U'}</div>
                      <div className="user-info">
                        <span className="user-name">{user.fullName}</span>
                        <span className="user-email">{user.email}</span>
                      </div>
                    </div>
                  </td>
                  <td><span className={`badge-role is-${user.role?.toLowerCase()}`}>{roles[user.role] ?? user.role}</span></td>
                  <td><span className={`badge-status is-${user.status?.toLowerCase()}`}>{user.status === 'Active' ? 'Hoạt động' : 'Đã khóa'}</span></td>
                  <td>{user.createdAt && new Date(user.createdAt).getFullYear() > 1 ? new Date(user.createdAt).toLocaleDateString('vi-VN') : '—'}</td>
                  <td>
                    <div className="admin-actions">
                      <button className="admin-action" disabled={busy} onClick={() => openUser(user)}>Sửa</button>
                      {user.role === 'Admin' ? (
                        <span className="admin-muted">Được bảo vệ</span>
                      ) : (
                        <button className="admin-action" disabled={busy} onClick={() => toggle(user)}>
                          {user.status === 'Active' ? 'Khóa' : 'Mở khóa'}
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!loading && !filteredUsers.length && <p className="admin-empty">Không tìm thấy tài khoản phù hợp.</p>}
        </div>
      )}

      {/* TAB 2: COURSES & ASSIGNMENT */}
      {tab === 'courses' && (
        <div>
          <div style={{
            background: '#eff6ff',
            border: '1px solid #bfdbfe',
            borderRadius: '12px',
            padding: '12px 16px',
            marginBottom: '18px',
            display: 'flex',
            alignItems: 'center',
            gap: '12px'
          }}>
            <span style={{ fontSize: '22px' }}>💡</span>
            <div style={{ fontSize: '13px', color: '#1e40af', lineHeight: 1.5 }}>
              <strong>Quy trình phân quyền môn học:</strong> Quản trị viên (Admin) tạo môn học theo chương trình đào tạo của Nhà trường và phân công Giảng viên phụ trách. Giảng viên được phân công sẽ có quyền tạo lớp học phần, ngân hàng câu hỏi và tổ chức kỳ thi.
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {filteredCourses.map(course => (
              <div className="assign-card" key={course.courseId} style={{ borderLeft: '4px solid #2563eb' }}>
                <div>
                  <div className="assign-course-title">
                    <span className="assign-course-code">{course.courseCode}</span>
                    {course.courseName}
                  </div>
                  <p className="assign-course-desc">{course.description || 'Chưa có mô tả chi tiết.'}</p>
                </div>
                <div className="assign-controls">
                  <label htmlFor={`lecturer-${course.courseId}`} style={{ fontWeight: 600, fontSize: '13px', color: '#475569' }}>
                    Giảng viên phụ trách:
                  </label>
                  <select
                    id={`lecturer-${course.courseId}`}
                    className="assign-select"
                    disabled={busy || loading}
                    value={course.lecturerId ?? ''}
                    onChange={e => void mutate(() => assignLecturerToCourse(course.courseId, Number(e.target.value)), 'Đã cập nhật phân công giảng viên.')}
                  >
                    <option value="" disabled>Chọn giảng viên</option>
                    {course.lecturerId && !lecturers.some(l => l.userId === course.lecturerId) && (
                      <option value={course.lecturerId} disabled>{course.lecturerName} (không hoạt động)</option>
                    )}
                    {lecturers.map(l => (
                      <option key={l.userId} value={l.userId}>{l.fullName} ({l.email})</option>
                    ))}
                  </select>
                </div>
              </div>
            ))}
          </div>

          {!loading && !filteredCourses.length && (
            <div className="admin-empty">
              <p>Chưa có môn học nào trong hệ thống.</p>
              <button className="admin-btn admin-btn--primary" style={{ marginTop: '8px' }} onClick={() => setModalCourse(true)}>
                + Thêm môn học đầu tiên
              </button>
            </div>
          )}
          <p className="admin-muted" style={{ marginTop: '12px' }}>
            * Lưu ý: Môn học đã có lớp học phần hoặc kỳ thi cần hoàn tất chuyển giao trước khi đổi giảng viên phụ trách.
          </p>
        </div>
      )}

      {/* TAB 3: AI & LANGUAGE CONFIGURATION */}
      {tab === 'settings' && settings && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          
          {/* AI Subsystems Status Cards */}
          <div>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '15px', fontWeight: 700, color: '#1e293b' }}>
              Trạng thái hệ thống trí tuệ nhân tạo (AI Engine)
            </h3>
            <div className="ai-overview-grid">
              <div className="ai-status-card">
                <div className="ai-status-card__top">
                  <span className="ai-status-card__icon">🧠</span>
                  <span className="ai-badge-active">
                    <span className="ai-badge-pulse" /> {configured(settings.providers.llm)}
                  </span>
                </div>
                <div>
                  <strong style={{ fontSize: '15px', color: '#0f172a' }}>LLM (Mô hình Ngôn ngữ)</strong>
                  <p style={{ margin: '4px 0 8px 0', fontSize: '12.5px', color: '#64748b' }}>
                    Sinh câu hỏi tự động, phân tích rubric và chấm điểm vấn đáp.
                  </p>
                  <code style={{ fontSize: '12px', background: '#f1f5f9', padding: '3px 8px', borderRadius: '6px', color: '#0369a1', fontWeight: 600 }}>
                    {settings.llmModel || 'gemini-2.5-flash'}
                  </code>
                </div>
              </div>

              <div className="ai-status-card">
                <div className="ai-status-card__top">
                  <span className="ai-status-card__icon">🎙️</span>
                  <span className="ai-badge-active">
                    <span className="ai-badge-pulse" /> {configured(settings.providers.stt)}
                  </span>
                </div>
                <div>
                  <strong style={{ fontSize: '15px', color: '#0f172a' }}>STT (Nhận diện giọng nói)</strong>
                  <p style={{ margin: '4px 0 8px 0', fontSize: '12.5px', color: '#64748b' }}>
                    Chuyển đổi âm thanh câu trả lời của sinh viên sang văn bản tức thì.
                  </p>
                  <code style={{ fontSize: '12px', background: '#f1f5f9', padding: '3px 8px', borderRadius: '6px', color: '#047857', fontWeight: 600 }}>
                    {settings.providers.liveModel || 'gemini-live-stream'}
                  </code>
                </div>
              </div>

              <div className="ai-status-card">
                <div className="ai-status-card__top">
                  <span className="ai-status-card__icon">🔊</span>
                  <span className="ai-badge-active">
                    <span className="ai-badge-pulse" /> {configured(settings.providers.tts)}
                  </span>
                </div>
                <div>
                  <strong style={{ fontSize: '15px', color: '#0f172a' }}>TTS (Tổng hợp giọng nói)</strong>
                  <p style={{ margin: '4px 0 8px 0', fontSize: '12.5px', color: '#64748b' }}>
                    Đọc câu hỏi vấn đáp với ngữ điệu tự nhiên, chân thực.
                  </p>
                  <code style={{ fontSize: '12px', background: '#f1f5f9', padding: '3px 8px', borderRadius: '6px', color: '#7c3aed', fontWeight: 600 }}>
                    {settings.providers.ttsModel || 'gemini-voice-tts'}
                  </code>
                </div>
              </div>
            </div>
          </div>

          {/* Interactive Speech & Language Control Center */}
          <div className="settings-card" style={{ border: '1.5px solid #cbd5e1' }}>
            <div className="settings-card__header">
              <div className="settings-card__icon" style={{ background: '#f0fdf4', color: '#059669', fontSize: '20px' }}>
                🌐
              </div>
              <div>
                <div className="settings-card__title">Cấu hình Ngôn ngữ & Giọng đọc phòng thi (STT / TTS)</div>
                <div className="settings-card__desc">
                  Thiết lập chuẩn giao tiếp giọng nói cho toàn bộ các phòng thi AI Viva Room trong hệ thống.
                </div>
              </div>
            </div>

            {/* Language Selection */}
            <div className="settings-group">
              <label className="settings-label">1. Ngôn ngữ giao tiếp & nhận diện giọng nói (Language)</label>
              <div className="settings-options-grid">
                {[
                  { code: 'vi-VN', flag: '🇻🇳', name: 'Tiếng Việt', desc: 'Chuẩn ngữ âm tiếng Việt (vi-VN), tối ưu hóa từ vựng chuyên ngành trong nước' },
                  { code: 'en-US', flag: '🇺🇸', name: 'Tiếng Anh', desc: 'Chuẩn giọng quốc tế (en-US), dùng cho các môn giảng dạy bằng tiếng Anh' }
                ].map(lang => {
                  const isSelected = (settings.language || 'vi-VN') === lang.code;
                  return (
                    <div
                      key={lang.code}
                      className={`lang-picker-option ${isSelected ? 'is-selected' : ''}`}
                      onClick={() => void mutate(() => updateSpeechSettings({ language: lang.code, voice: settings.voice }), `Đã chuyển sang ${lang.name} (${lang.code}).`)}
                    >
                      <span className="lang-flag">{lang.flag}</span>
                      <div className="lang-text">
                        <span className="lang-name">{lang.name} ({lang.code})</span>
                        <span className="lang-code">{lang.desc}</span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Voice Selection */}
            <div className="settings-group" style={{ marginTop: '20px' }}>
              <label className="settings-label">2. Giọng đọc giám khảo AI (TTS Voice Persona)</label>
              <div className="voice-picker-grid">
                {[
                  { id: 'Kore', label: 'Kore', icon: '🎙️', desc: 'Trầm ấm, uy tín' },
                  { id: 'Aoede', label: 'Aoede', icon: '🎵', desc: 'Truyền cảm, tự nhiên' },
                  { id: 'Puck', label: 'Puck', icon: '⚡', desc: 'Tươi sáng, năng động' },
                  { id: 'Charon', label: 'Charon', icon: '🏛️', desc: 'Điềm đạm, rõ ràng' },
                  { id: 'Fenrir', label: 'Fenrir', icon: '🛡️', desc: 'Mạnh mẽ, dứt khoát' }
                ].map(v => {
                  const isSelected = (settings.voice || 'Kore') === v.id;
                  return (
                    <div
                      key={v.id}
                      className={`voice-picker-item ${isSelected ? 'is-selected' : ''}`}
                      onClick={() => void mutate(() => updateSpeechSettings({ language: settings.language || 'vi-VN', voice: v.id }), `Đã chọn giọng đọc ${v.label}.`)}
                    >
                      <span style={{ fontSize: '20px', marginBottom: '4px' }}>{v.icon}</span>
                      <strong style={{ fontSize: '14px', color: isSelected ? '#1d4ed8' : '#1e293b' }}>{v.label}</strong>
                      <small style={{ fontSize: '11px', color: '#64748b' }}>{v.desc}</small>
                      {isSelected && (
                        <span style={{ marginTop: '4px', fontSize: '10px', background: '#2563eb', color: '#fff', padding: '2px 6px', borderRadius: '4px', fontWeight: 700 }}>
                          ĐANG CHỌN
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Live Indicator */}
            <div style={{ marginTop: '18px', padding: '12px 16px', background: '#f8fafc', borderRadius: '10px', border: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px' }}>
              <div style={{ fontSize: '13px', color: '#334155' }}>
                Đang áp dụng: <strong>{settings.language === 'en-US' ? '🇺🇸 Tiếng Anh (en-US)' : '🇻🇳 Tiếng Việt (vi-VN)'}</strong> · Giọng đọc: <strong>🎙️ {settings.voice}</strong> · Môi trường: <strong>{settings.providers.configurationEnvironment}</strong>
              </div>
              <span style={{ fontSize: '12px', color: '#059669', fontWeight: 600 }}>
                ● Áp dụng tức thì cho phòng thi
              </span>
            </div>
          </div>

          <div style={{ fontSize: '12.5px', color: '#64748b', lineHeight: 1.6 }}>
            File cấu hình hệ thống: <code>{settings.configurationFile}</code>. Dữ liệu bảo mật (API Key) được mã hóa an toàn ở tầng backend.
          </div>
        </div>
      )}

      {/* USER MODAL */}
      {modal && (
        <div className="modal-overlay">
          <section className="modal-card admin-user-modal" role="dialog" aria-modal="true" aria-labelledby="admin-user-title">
            <div className="modal-header">
              <h2 id="admin-user-title">{modal.userId ? 'Sửa tài khoản' : 'Tạo tài khoản mới'}</h2>
              <button type="button" className="modal-close" aria-label="Đóng" disabled={busy} onClick={() => setModal(null)}>×</button>
            </div>
            <form onSubmit={saveUser}>
              <div className="modal-body">
                {error && <div className="admin-error" role="alert">{error}</div>}
                <div className="modal-field">
                  <label className="modal-label" htmlFor="admin-name">Họ và tên</label>
                  <input id="admin-name" className="modal-input" autoFocus required maxLength={150} disabled={busy} value={form.fullName}
                    onChange={e => setForm({ ...form, fullName: e.target.value })} placeholder="Ví dụ: Nguyễn Văn A" />
                </div>
                <div className="modal-field">
                  <label className="modal-label" htmlFor="admin-email">Email {modal.userId ? '(không đổi)' : ''}</label>
                  <input id="admin-email" className="modal-input" type="email" autoComplete="off" required maxLength={255} disabled={busy || !!modal.userId}
                    value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} placeholder="ten@truong.edu.vn" />
                </div>
                <div className="modal-field">
                  <label className="modal-label" htmlFor="admin-role">Vai trò hệ thống</label>
                  <select id="admin-role" className="modal-input" disabled={busy || modal.role === 'Admin'} value={form.role}
                    onChange={e => setForm({ ...form, role: e.target.value })}>
                    {Object.entries(roles).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                  </select>
                </div>
                {!modal.userId && (
                  <div className="modal-field">
                    <label className="modal-label" htmlFor="admin-password">Mật khẩu khởi tạo</label>
                    <input id="admin-password" className="modal-input" type="password" autoComplete="new-password" required minLength={8} disabled={busy}
                      placeholder="Tối thiểu 8 ký tự" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} />
                  </div>
                )}
              </div>
              <div className="modal-footer">
                <button type="button" className="admin-btn admin-btn--ghost" disabled={busy} onClick={() => setModal(null)}>Hủy</button>
                <button type="submit" className="admin-btn admin-btn--primary" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu tài khoản'}</button>
              </div>
            </form>
          </section>
        </div>
      )}

      {/* COURSE CREATION MODAL (ADMIN ONLY) */}
      {modalCourse && (
        <div className="modal-overlay">
          <section className="modal-card admin-user-modal" role="dialog" aria-modal="true" aria-labelledby="admin-course-title">
            <div className="modal-header">
              <h2 id="admin-course-title">🏛️ Thêm môn học mới & Phân quyền</h2>
              <button type="button" className="modal-close" aria-label="Đóng" disabled={busy} onClick={() => setModalCourse(false)}>×</button>
            </div>
            <form onSubmit={saveCourse}>
              <div className="modal-body">
                {error && <div className="admin-error" role="alert">{error}</div>}
                <div className="modal-field">
                  <label className="modal-label" htmlFor="course-code">Mã môn học *</label>
                  <input
                    id="course-code"
                    className="modal-input"
                    autoFocus
                    required
                    maxLength={50}
                    disabled={busy}
                    value={courseForm.courseCode}
                    onChange={e => setCourseForm({ ...courseForm, courseCode: e.target.value.toUpperCase() })}
                    placeholder="Ví dụ: SWD392, PRN231, CSD201"
                  />
                  <small style={{ fontSize: '11px', color: '#64748b' }}>Gồm chữ cái viết hoa, số và dấu gạch dưới.</small>
                </div>

                <div className="modal-field">
                  <label className="modal-label" htmlFor="course-name">Tên môn học *</label>
                  <input
                    id="course-name"
                    className="modal-input"
                    required
                    maxLength={200}
                    disabled={busy}
                    value={courseForm.courseName}
                    onChange={e => setCourseForm({ ...courseForm, courseName: e.target.value })}
                    placeholder="Ví dụ: Kiến trúc và Thiết kế Phần mềm"
                  />
                </div>

                <div className="modal-field">
                  <label className="modal-label" htmlFor="course-lecturer">Giảng viên phụ trách ban đầu *</label>
                  <select
                    id="course-lecturer"
                    className="modal-input"
                    required
                    disabled={busy || !lecturers.length}
                    value={courseForm.lecturerId}
                    onChange={e => setCourseForm({ ...courseForm, lecturerId: e.target.value })}
                  >
                    {!lecturers.length && <option value="">Chưa có tài khoản giảng viên hoạt động</option>}
                    {lecturers.map(l => (
                      <option key={l.userId} value={l.userId}>{l.fullName} ({l.email})</option>
                    ))}
                  </select>
                  <small style={{ fontSize: '11px', color: '#64748b' }}>Admin có thể chuyển đổi giảng viên phụ trách bất kỳ lúc nào.</small>
                </div>

                <div className="modal-field">
                  <label className="modal-label" htmlFor="course-desc">Mô tả học phần</label>
                  <textarea
                    id="course-desc"
                    className="modal-input"
                    rows={3}
                    maxLength={1000}
                    disabled={busy}
                    value={courseForm.description}
                    onChange={e => setCourseForm({ ...courseForm, description: e.target.value })}
                    placeholder="Mục tiêu môn học, chuẩn đầu ra..."
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button type="button" className="admin-btn admin-btn--ghost" disabled={busy} onClick={() => setModalCourse(false)}>Hủy</button>
                <button type="submit" className="admin-btn admin-btn--primary" disabled={busy || !lecturers.length}>
                  {busy ? 'Đang khởi tạo…' : 'Khởi tạo & Phân quyền'}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </Sidebar>
  );
}
