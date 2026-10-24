import { useCallback, useEffect, useMemo, useState } from 'react';
import Sidebar from '../components/Sidebar.jsx';
import { getAdminUsers, createAdminUser, updateAdminUser, toggleUserStatus,
  getAdminCourses, assignLecturerToCourse, getSpeechSettings } from '../api/admin.js';
import '../styles/AdminManagement.css';

const roles = { Admin: 'Quản trị viên', Lecturer: 'Giảng viên', Student: 'Sinh viên' };
const emptyForm = { fullName: '', email: '', password: '', role: 'Lecturer' };
const configured = value => value === 'Configured' ? 'Đã cấu hình' : 'Chưa cấu hình';
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
  const [modal, setModal] = useState(null);
  const [form, setForm] = useState(emptyForm);

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
  const filteredCourses = courses.filter(c => `${c.courseCode} ${c.courseName} ${c.lecturerName ?? ''}`
    .toLocaleLowerCase('vi').includes(search.trim().toLocaleLowerCase('vi')));

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
  const toggle = user => {
    const action = user.status === 'Active' ? 'khóa' : 'mở khóa';
    if (window.confirm(`Bạn muốn ${action} tài khoản ${user.email}?`))
      void mutate(() => toggleUserStatus(user.userId), `Đã ${action} tài khoản.`);
  };

  return <Sidebar title="Quản trị hệ thống" subtitle="Tài khoản, phân công môn học và cấu hình dịch vụ."
    actions={tab === 'users' && <button className="admin-btn admin-btn--primary" disabled={busy || loading}
      onClick={() => openUser(null)}>+ Tạo tài khoản</button>}>
    {error && !modal && <div className="admin-error" role="alert">{error}</div>}
    {notice && <div className="toast-success" role="status">{notice}</div>}
    <div className="admin-tabs" aria-label="Chức năng quản trị">
      {[['users', 'Tài khoản'], ['courses', 'Phân công giảng viên'], ['settings', 'Cấu hình AI']].map(([id, label]) =>
        <button key={id} className={`admin-tab-btn ${tab === id ? 'is-active' : ''}`} disabled={busy}
          onClick={() => { setTab(id); setSearch(''); setNotice(''); }}>{label}</button>)}
    </div>
    <div className="admin-toolbar">
      {tab !== 'settings' && <div className="admin-filters">
        <input className="admin-search" aria-label="Tìm kiếm" placeholder={tab === 'users' ? 'Tìm tên hoặc email…' : 'Tìm môn học hoặc giảng viên…'}
          value={search} onChange={e => setSearch(e.target.value)} />
        {tab === 'users' && <select className="admin-select" aria-label="Lọc vai trò" value={roleFilter} onChange={e => setRoleFilter(e.target.value)}>
          <option value="">Tất cả vai trò</option>
          {Object.entries(roles).map(([value, name]) => <option key={value} value={value}>{name}</option>)}
        </select>}
      </div>}
      <button className="admin-btn admin-btn--ghost" disabled={loading || busy} onClick={() => { setLoading(true); void load(); }}>{loading ? 'Đang tải…' : 'Tải lại'}</button>
    </div>
    {loading && <p role="status">Đang tải dữ liệu từ máy chủ…</p>}
    {tab === 'users' && <div className="admin-table-wrap">
      <table className="admin-table"><thead><tr><th>Người dùng</th><th>Vai trò</th><th>Trạng thái</th><th>Ngày tạo</th><th>Thao tác</th></tr></thead>
        <tbody>{filteredUsers.map(user => <tr key={user.userId}>
          <td><div className="user-info"><span className="user-name">{user.fullName}</span><span className="user-email">{user.email}</span></div></td>
          <td><span className={`badge-role is-${user.role?.toLowerCase()}`}>{roles[user.role] ?? user.role}</span></td>
          <td><span className={`badge-status is-${user.status?.toLowerCase()}`}>{user.status === 'Active' ? 'Hoạt động' : 'Đã khóa'}</span></td>
          <td>{user.createdAt && new Date(user.createdAt).getFullYear() > 1 ? new Date(user.createdAt).toLocaleDateString('vi-VN') : '—'}</td>
          <td><div className="admin-actions">
            <button className="admin-action" disabled={busy} onClick={() => openUser(user)}>Sửa</button>
            {user.role === 'Admin' ? <span className="admin-muted">Được bảo vệ</span> :
              <button className="admin-action" disabled={busy} onClick={() => toggle(user)}>{user.status === 'Active' ? 'Khóa' : 'Mở khóa'}</button>}
          </div></td>
        </tr>)}</tbody></table>
      {!loading && !filteredUsers.length && <p className="admin-empty">Không tìm thấy tài khoản.</p>}
    </div>}
    {tab === 'courses' && <div>
      {filteredCourses.map(course => <div className="assign-card" key={course.courseId}>
        <div><div className="assign-course-title"><span className="assign-course-code">{course.courseCode}</span>{course.courseName}</div>
          <p className="assign-course-desc">{course.description || 'Chưa có mô tả.'}</p></div>
        <div className="assign-controls"><label htmlFor={`lecturer-${course.courseId}`}>Giảng viên</label>
          <select id={`lecturer-${course.courseId}`} className="assign-select" disabled={busy || loading} value={course.lecturerId ?? ''}
            onChange={e => void mutate(() => assignLecturerToCourse(course.courseId, Number(e.target.value)), 'Đã cập nhật phân công.')}>
            <option value="" disabled>Chọn giảng viên</option>
            {course.lecturerId && !lecturers.some(l => l.userId === course.lecturerId)
              && <option value={course.lecturerId} disabled>{course.lecturerName} (không hoạt động)</option>}
            {lecturers.map(l => <option key={l.userId} value={l.userId}>{l.fullName} ({l.email})</option>)}
          </select></div>
      </div>)}
      {!loading && !filteredCourses.length && <p className="admin-empty">Không tìm thấy môn học.</p>}
      <p className="admin-muted">Môn đã có lớp hoặc kỳ thi cần chuyển giao nghiệp vụ trước khi đổi giảng viên.</p>
    </div>}
    {tab === 'settings' && settings && <section className="admin-config">
      <h2>Cấu hình backend hiện tại</h2>
      <div className="admin-config-grid">
        {[['LLM', settings.providers.llm, settings.llmModel], ['STT', settings.providers.stt, settings.providers.liveModel],
          ['TTS', settings.providers.tts, settings.providers.ttsModel]].map(([label, state, model]) =>
          <div className="admin-config-item" key={label}><strong>{label}</strong><span>{configured(state)}</span><code>{model || 'Chưa đặt model'}</code></div>)}
      </div>
      <p>Giọng đọc: <strong>{settings.voice}</strong> · Môi trường: <strong>{settings.providers.configurationEnvironment}</strong></p>
      <p>Chỉnh cấu hình tại <code>{settings.configurationFile}</code> trên máy chạy backend, rồi khởi động lại backend.
        Biến môi trường có thể ghi đè giá trị trong file. API key không hiển thị trên trình duyệt.</p>
      {!!settings.providers.missing?.length && <p>Còn thiếu: <code>{settings.providers.missing.join(', ')}</code></p>}
      <p className="admin-muted">Trạng thái chỉ xác nhận có cấu hình; chưa kiểm tra key, model hay hạn mức với nhà cung cấp. Trang này không gọi AI.</p>
    </section>}
    {modal && <div className="modal-overlay"><section className="modal-card admin-user-modal" role="dialog" aria-modal="true" aria-labelledby="admin-user-title">
      <div className="modal-header"><h2 id="admin-user-title">{modal.userId ? 'Sửa tài khoản' : 'Tạo tài khoản'}</h2>
        <button type="button" className="modal-close" aria-label="Đóng" disabled={busy} onClick={() => setModal(null)}>×</button></div>
      <form onSubmit={saveUser}>
        <div className="modal-body">
          {error && <div className="admin-error" role="alert">{error}</div>}
          <div className="modal-field"><label className="modal-label" htmlFor="admin-name">Họ tên</label>
            <input id="admin-name" className="modal-input" autoFocus required maxLength={150} disabled={busy} value={form.fullName}
              onChange={e => setForm({ ...form, fullName: e.target.value })} /></div>
          <div className="modal-field"><label className="modal-label" htmlFor="admin-email">Email {modal.userId ? '(không đổi)' : ''}</label>
            <input id="admin-email" className="modal-input" type="email" autoComplete="off" required maxLength={255} disabled={busy || !!modal.userId}
              value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /></div>
          <div className="modal-field"><label className="modal-label" htmlFor="admin-role">Vai trò</label>
            <select id="admin-role" className="modal-input" disabled={busy || modal.role === 'Admin'} value={form.role}
              onChange={e => setForm({ ...form, role: e.target.value })}>
              {Object.entries(roles).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select></div>
          {!modal.userId && <div className="modal-field"><label className="modal-label" htmlFor="admin-password">Mật khẩu ban đầu</label>
            <input id="admin-password" className="modal-input" type="password" autoComplete="new-password" required minLength={8} disabled={busy}
              placeholder="Ít nhất 8 ký tự" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} /></div>}
        </div>
        <div className="modal-footer"><button type="button" className="admin-btn admin-btn--ghost" disabled={busy} onClick={() => setModal(null)}>Hủy</button>
          <button type="submit" className="admin-btn admin-btn--primary" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu tài khoản'}</button></div>
      </form>
    </section></div>}
  </Sidebar>;
}
