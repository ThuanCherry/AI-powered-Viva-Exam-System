import { useEffect, useMemo, useState } from "react";
import Sidebar from "../components/Sidebar.jsx";
import { getSession } from "../auth/session.js";
import {
  getAdminUsers,
  createAdminUser,
  updateAdminUser,
  toggleUserStatus,
  getAdminCourses,
  assignLecturerToCourse,
  getSpeechSettings,
  saveSpeechSettings,
} from "../api/admin.js";
import "../styles/AdminManagement.css";

export default function AdminManagement() {
  const currentSession = getSession();
  const [activeTab, setActiveTab] = useState("users"); // 'users' | 'courses' | 'settings'

  // --- TAB 1: USERS STATE ---
  const [users, setUsers] = useState([]);
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [userSearch, setUserSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("ALL");
  const [isAddUserModalOpen, setIsAddUserModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState(null);

  const [userForm, setUserForm] = useState({
    fullName: "",
    email: "",
    role: "Lecturer",
    password: "",
  });

  // --- TAB 2: COURSES STATE ---
  const [courses, setCourses] = useState([]);
  const [loadingCourses, setLoadingCourses] = useState(true);
  const [courseSearch, setCourseSearch] = useState("");
  const [assigningId, setAssigningId] = useState(null);

  // --- TAB 3: SETTINGS STATE ---
  const [settings, setSettings] = useState({
    sttLanguage: "vi-VN",
    sttAutoPunctuation: true,
    ttsLanguage: "vi-VN",
    ttsVoiceGender: "female",
    ttsSpeedRate: 1.0,
    ttsPitch: 1.0,
    silenceThresholdSeconds: 3,
  });
  const [savingSettings, setSavingSettings] = useState(false);
  const [toastMsg, setToastMsg] = useState(null);
  const [isPlayingTest, setIsPlayingTest] = useState(false);

  // Fetch initial data
  const loadUsersData = async () => {
    setLoadingUsers(true);
    try {
      const data = await getAdminUsers();
      setUsers(data ?? []);
    } catch {
      // Handled
    } finally {
      setLoadingUsers(false);
    }
  };

  const loadCoursesData = async () => {
    setLoadingCourses(true);
    try {
      const data = await getAdminCourses();
      setCourses(data ?? []);
    } catch {
      // Handled
    } finally {
      setLoadingCourses(false);
    }
  };

  const loadSettingsData = async () => {
    try {
      const data = await getSpeechSettings();
      setSettings(data);
    } catch {
      // Handled
    }
  };

  useEffect(() => {
    // Chỉ xóa mock users cũ nếu còn sót lại
    localStorage.removeItem("aives_admin_users");

    loadUsersData();
    loadCoursesData();
    loadSettingsData();
  }, []);

  const lecturersList = useMemo(() => {
    return users.filter((u) => u.role === "Lecturer" && u.status !== "Inactive");
  }, [users]);

  // Tab 1 filters
  const filteredUsers = useMemo(() => {
    const term = userSearch.toLowerCase().trim();
    return users.filter((u) => {
      const matchSearch =
        !term ||
        u.fullName?.toLowerCase().includes(term) ||
        u.email?.toLowerCase().includes(term);
      const matchRole =
        roleFilter === "ALL" || u.role?.toUpperCase() === roleFilter.toUpperCase();
      return matchSearch && matchRole;
    });
  }, [users, userSearch, roleFilter]);

  // Tab 2 filters
  const filteredCourses = useMemo(() => {
    const term = courseSearch.toLowerCase().trim();
    return courses.filter((c) => {
      return (
        !term ||
        c.courseCode?.toLowerCase().includes(term) ||
        c.courseName?.toLowerCase().includes(term) ||
        c.lecturerName?.toLowerCase().includes(term)
      );
    });
  }, [courses, courseSearch]);

  const showToast = (msg) => {
    setToastMsg(msg);
    setTimeout(() => setToastMsg(null), 3500);
  };

  // User Actions
  const handleOpenAddUser = () => {
    setUserForm({ fullName: "", email: "", role: "Lecturer", password: "" });
    setIsAddUserModalOpen(true);
  };

  const handleOpenEditUser = (user) => {
    setEditingUser(user);
    setUserForm({
      fullName: user.fullName || "",
      email: user.email || "",
      role: user.role || "Lecturer",
      password: "",
    });
  };

  const handleSaveUser = async (e) => {
    e.preventDefault();
    if (!userForm.fullName.trim() || !userForm.email.trim()) {
      alert("Please fill in both full name and email.");
      return;
    }

    try {
      if (editingUser) {
        await updateAdminUser(editingUser.userId, {
          fullName: userForm.fullName,
          role: userForm.role,
        });
        showToast("User details updated successfully!");
        setEditingUser(null);
      } else {
        await createAdminUser(userForm);
        showToast("New user account created successfully!");
        setIsAddUserModalOpen(false);
      }
      loadUsersData();
    } catch (err) {
      alert(err.message || "Operation failed.");
    }
  };

  const handleToggleStatus = async (user) => {
    if (user.role === "Admin") {
      alert("Administrator accounts are protected and cannot be deactivated.");
      return;
    }
    const action = user.status === "Active" ? "Deactivate" : "Activate";
    if (!window.confirm(`Are you sure you want to ${action.toLowerCase()} account "${user.fullName}"?`)) return;

    try {
      await toggleUserStatus(user.userId);
      showToast(`User account ${action.toLowerCase()}d successfully!`);
      loadUsersData();
    } catch (err) {
      alert(err.message || "Failed to update status.");
    }
  };

  // Course Assignment Action
  const handleAssignLecturer = async (courseId, lecturerId) => {
    setAssigningId(courseId);
    try {
      const lecturer = lecturersList.find((l) => String(l.userId) === String(lecturerId));
      const lecturerName = lecturer ? lecturer.fullName : "Unassigned";

      await assignLecturerToCourse(courseId, lecturerId ? Number(lecturerId) : null, lecturerName);
      showToast(`Assigned course to lecturer "${lecturerName}" successfully!`);
      loadCoursesData();
    } catch (err) {
      alert(err.message || "Failed to assign course.");
    } finally {
      setAssigningId(null);
    }
  };

  // Settings Actions
  const handleSaveSettings = async () => {
    setSavingSettings(true);
    try {
      await saveSpeechSettings(settings);
      showToast("Speech and language settings saved successfully!");
    } catch (err) {
      alert(err.message || "Failed to save settings.");
    } finally {
      setSavingSettings(false);
    }
  };

  const handleTestAudio = () => {
    if (!("speechSynthesis" in window)) {
      alert("Your browser does not support the Speech Synthesis API.");
      return;
    }

    window.speechSynthesis.cancel();
    setIsPlayingTest(true);

    const isVietnamese = settings.ttsLanguage === "vi-VN";
    const sampleText = isVietnamese
      ? "Xin chào bạn. Đây là giọng đọc mẫu thử nghiệm của Hệ thống Viva Exam AIVES."
      : "Hello and welcome. This is a voice test for the AIVES Viva Examination System.";

    const utterance = new SpeechSynthesisUtterance(sampleText);
    utterance.lang = settings.ttsLanguage;
    utterance.rate = settings.ttsSpeedRate;
    utterance.pitch = settings.ttsPitch;

    utterance.onend = () => setIsPlayingTest(false);
    utterance.onerror = () => setIsPlayingTest(false);

    window.speechSynthesis.speak(utterance);
  };

  return (
    <Sidebar
      title="System Administration"
      subtitle="Manage user accounts, assign lecturers to courses, and configure STT/TTS language models."
      actions={
        activeTab === "users" ? (
          <button
            type="button"
            className="qc-btn qc-btn--primary"
            onClick={handleOpenAddUser}
          >
            + New user
          </button>
        ) : null
      }
    >
      {/* Toast Notification */}
      {toastMsg && (
        <div className="toast-success" style={{ marginBottom: "1rem" }}>
          <svg viewBox="0 0 20 20" width="18" height="18" fill="currentColor">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
          </svg>
          <span>{toastMsg}</span>
        </div>
      )}

      {/* Admin Tabs */}
      <div className="admin-tabs">
        <button
          type="button"
          className={`admin-tab-btn ${activeTab === "users" ? "is-active" : ""}`}
          onClick={() => setActiveTab("users")}
        >
          <svg viewBox="0 0 20 20" width="16" height="16" fill="currentColor">
            <path d="M9 6a3 3 0 11-6 0 3 3 0 016 0zM17 6a3 3 0 11-6 0 3 3 0 016 0zM12.93 17c.046-.327.07-.66.07-1a6.97 6.97 0 00-1.5-4.33A5 5 0 0119 16v1h-6.07zM6 11a5 5 0 015 5v1H1v-1a5 5 0 015-5z" />
          </svg>
          User Management
          <span className="admin-tab-count">{users.length}</span>
        </button>

        <button
          type="button"
          className={`admin-tab-btn ${activeTab === "courses" ? "is-active" : ""}`}
          onClick={() => setActiveTab("courses")}
        >
          <svg viewBox="0 0 20 20" width="16" height="16" fill="currentColor">
            <path d="M10.394 2.08a1 1 0 00-.788 0l-7 3a1 1 0 000 1.84L5.25 8.051a10.912 10.912 0 01.352 4.198 1 1 0 00.998.901h6.8a1 1 0 00.998-.901 10.912 10.912 0 01.352-4.198l2.644-1.131a1 1 0 000-1.84l-7-3z" />
            <path d="M4.332 9.427a1 1 0 00-.832.973V15.5a1 1 0 00.6.915l5.5 2.5a1 1 0 00.8 0l5.5-2.5a1 1 0 00.6-.915v-5.1a1 1 0 00-.832-.973l-5.668 2.43a1 1 0 01-.8 0l-5.668-2.43z" />
          </svg>
          Course & Lecturer Assignment
          <span className="admin-tab-count">{courses.length}</span>
        </button>

        <button
          type="button"
          className={`admin-tab-btn ${activeTab === "settings" ? "is-active" : ""}`}
          onClick={() => setActiveTab("settings")}
        >
          <svg viewBox="0 0 20 20" width="16" height="16" fill="currentColor">
            <path fillRule="evenodd" d="M11.49 3.17c-.38-1.56-2.6-1.56-2.98 0a1.532 1.532 0 01-2.286.948c-1.372-.836-2.942.734-2.106 2.106.54.886.061 2.042-.947 2.287-1.561.379-1.561 2.6 0 2.978a1.532 1.532 0 01.947 2.287c-.836 1.372.734 2.942 2.106 2.106a1.532 1.532 0 012.287.947c.379 1.561 2.6 1.561 2.978 0a1.533 1.533 0 012.287-.947c1.372.836 2.942-.734 2.106-2.106a1.533 1.533 0 01.947-2.287c1.561-.379 1.561-2.6 0-2.978a1.532 1.532 0 01-.947-2.287c.836-1.372-.734-2.942-2.106-2.106a1.532 1.532 0 01-2.287-.947zM10 13a3 3 0 100-6 3 3 0 000 6z" clipRule="evenodd" />
          </svg>
          STT / TTS & System Settings
        </button>
      </div>

      {/* TAB 1: USER MANAGEMENT */}
      {activeTab === "users" && (
        <div>
          <div className="exams-toolbar">
            <div className="exams-filters">
              <input
                type="text"
                className="exams-search"
                placeholder="Search by user name or email..."
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
              />

              <select
                className="exams-select"
                value={roleFilter}
                onChange={(e) => setRoleFilter(e.target.value)}
              >
                <option value="ALL">All Roles</option>
                <option value="ADMIN">Admin</option>
                <option value="LECTURER">Lecturer</option>
                <option value="STUDENT">Student</option>
              </select>
            </div>

            <button type="button" className="qc-btn qc-btn--ghost" onClick={loadUsersData}>
              Refresh
            </button>
          </div>

          <div className="exams-table-wrap">
            {loadingUsers ? (
              <div className="exams-empty">
                <p>Loading users...</p>
              </div>
            ) : filteredUsers.length === 0 ? (
              <div className="exams-empty">
                <p>No user accounts found matching your criteria.</p>
              </div>
            ) : (
              <table className="exams-table">
                <thead>
                  <tr>
                    <th>User</th>
                    <th>Role</th>
                    <th>Status</th>
                    <th>Created At</th>
                    <th style={{ textAlign: "right" }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredUsers.map((u) => {
                    const initials = (u.fullName || "User")
                      .split(" ")
                      .map((n) => n[0])
                      .slice(0, 2)
                      .join("")
                      .toUpperCase();
                    const isInactive = u.status === "Inactive";

                    return (
                      <tr key={u.userId}>
                        <td>
                          <div className="user-cell">
                            <div className="user-avatar">{initials}</div>
                            <div className="user-info">
                              <span className="user-name">{u.fullName}</span>
                              <span className="user-email">{u.email}</span>
                            </div>
                          </div>
                        </td>
                        <td>
                          <span className={`badge-role is-${(u.role || "student").toLowerCase()}`}>
                            {u.role}
                          </span>
                        </td>
                        <td>
                          <span className={`badge-status is-${(u.status || "active").toLowerCase()}`}>
                            {u.status || "Active"}
                          </span>
                        </td>
                        <td>
                          {u.createdAt ? new Date(u.createdAt).toLocaleDateString() : "N/A"}
                        </td>
                        <td>
                          <div className="exams-actions" style={{ justifyContent: "flex-end" }}>
                            <button
                              type="button"
                              className="action-btn-link"
                              onClick={() => handleOpenEditUser(u)}
                            >
                              Edit
                            </button>
                            {u.role === "Admin" ? (
                              <span
                                style={{
                                  fontSize: "0.78rem",
                                  color: "#94a3b8",
                                  padding: "0.35rem 0.65rem",
                                  fontWeight: 500,
                                  background: "#f8fafc",
                                  border: "1px solid #e2e8f0",
                                  borderRadius: "0.375rem",
                                  cursor: "not-allowed",
                                }}
                                title="Administrator accounts are protected and cannot be deactivated."
                              >
                                🔒 Protected
                              </span>
                            ) : (
                              <button
                                type="button"
                                className={isInactive ? "qc-btn qc-btn--ghost" : "action-btn-cancel"}
                                style={{ fontSize: "0.825rem", padding: "0.35rem 0.65rem" }}
                                onClick={() => handleToggleStatus(u)}
                              >
                                {isInactive ? "Activate" : "Deactivate"}
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* TAB 2: COURSE & LECTURER ASSIGNMENT */}
      {activeTab === "courses" && (
        <div>
          <div className="exams-toolbar">
            <div className="exams-filters">
              <input
                type="text"
                className="exams-search"
                placeholder="Search courses by code or title..."
                value={courseSearch}
                onChange={(e) => setCourseSearch(e.target.value)}
              />
            </div>
            <button type="button" className="qc-btn qc-btn--ghost" onClick={loadCoursesData}>
              Refresh
            </button>
          </div>

          <div>
            {loadingCourses ? (
              <div className="exams-empty">
                <p>Loading course list...</p>
              </div>
            ) : filteredCourses.length === 0 ? (
              <div className="exams-empty">
                <p>No courses found matching your query.</p>
              </div>
            ) : (
              filteredCourses.map((c) => {
                const assigned = lecturersList.find((l) => l.fullName === c.lecturerName);
                const currentLecturerId = c.lecturerId || (assigned ? assigned.userId : "");

                return (
                  <div key={c.courseId} className="assign-card">
                    <div>
                      <div className="assign-course-title">
                        <span className="assign-course-code">{c.courseCode}</span>
                        {c.courseName}
                      </div>
                      <div className="assign-course-desc">
                        {c.description || "No description provided for this course."}
                      </div>
                    </div>

                    <div className="assign-controls">
                      <label style={{ fontSize: "0.85rem", fontWeight: 600, color: "#475569" }}>
                        Assigned Lecturer:
                      </label>
                      <select
                        className="assign-select"
                        value={currentLecturerId || ""}
                        disabled={assigningId === c.courseId}
                        onChange={(e) => handleAssignLecturer(c.courseId, e.target.value)}
                      >
                        <option value="">-- Unassigned --</option>
                        {lecturersList.map((l) => (
                          <option key={l.userId} value={l.userId}>
                            {l.fullName} ({l.email})
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}

      {/* TAB 3: STT / TTS & SYSTEM CONFIGURATION */}
      {activeTab === "settings" && (
        <div>
          <div className="settings-grid">
            {/* STT Configuration Card */}
            <div className="settings-card">
              <div className="settings-card__header">
                <div className="settings-card__icon">
                  <svg viewBox="0 0 20 20" width="22" height="22" fill="currentColor">
                    <path fillRule="evenodd" d="M7 4a3 3 0 016 0v4a3 3 0 11-6 0V4zm4 10.93A7.001 7.001 0 0017 8a1 1 0 10-2 0A5 5 0 015 8a1 1 0 00-2 0 7.001 7.001 0 006 6.93V17H6a1 1 0 100 2h8a1 1 0 100-2h-3v-2.07z" clipRule="evenodd" />
                  </svg>
                </div>
                <div>
                  <h3 className="settings-card__title">Speech-to-Text (STT) Configuration</h3>
                  <p className="settings-card__desc">Recognizes oral candidate responses during viva sessions</p>
                </div>
              </div>

              <div className="settings-group">
                <span className="settings-label">Primary Recognition Language</span>
                <div className="settings-options-grid">
                  <div
                    className={`lang-picker-option ${settings.sttLanguage === "vi-VN" ? "is-selected" : ""}`}
                    onClick={() => setSettings((s) => ({ ...s, sttLanguage: "vi-VN" }))}
                  >
                    <span className="lang-flag">🇻🇳</span>
                    <div className="lang-text">
                      <span className="lang-name">Vietnamese</span>
                      <span className="lang-code">vi-VN</span>
                    </div>
                  </div>

                  <div
                    className={`lang-picker-option ${settings.sttLanguage === "en-US" ? "is-selected" : ""}`}
                    onClick={() => setSettings((s) => ({ ...s, sttLanguage: "en-US" }))}
                  >
                    <span className="lang-flag">🇺🇸</span>
                    <div className="lang-text">
                      <span className="lang-name">English</span>
                      <span className="lang-code">en-US</span>
                    </div>
                  </div>
                </div>
              </div>

              <div className="settings-group">
                <label className="settings-label">Silence Threshold</label>
                <div className="settings-slider-wrap">
                  <input
                    type="range"
                    min="1"
                    max="10"
                    step="1"
                    className="settings-slider"
                    value={settings.silenceThresholdSeconds}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, silenceThresholdSeconds: Number(e.target.value) }))
                    }
                  />
                  <span className="settings-slider-val">{settings.silenceThresholdSeconds}s</span>
                </div>
                <p style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.25rem" }}>
                  Candidate turns are automatically submitted when silence exceeds this threshold.
                </p>
              </div>

              <div className="settings-group">
                <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
                  <input
                    type="checkbox"
                    checked={settings.sttAutoPunctuation}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, sttAutoPunctuation: e.target.checked }))
                    }
                  />
                  <strong>Enable automated punctuation & formatting</strong>
                </label>
              </div>
            </div>

            {/* TTS Configuration Card */}
            <div className="settings-card">
              <div className="settings-card__header">
                <div className="settings-card__icon" style={{ background: "#fdf2f8", color: "#db2777" }}>
                  <svg viewBox="0 0 20 20" width="22" height="22" fill="currentColor">
                    <path fillRule="evenodd" d="M9.383 3.076A1 1 0 0110 4v12a1 1 0 01-1.707.707L4.586 13H2a1 1 0 01-1-1V8a1 1 0 011-1h2.586l3.707-3.707a1 1 0 011.09-.217zM14.657 2.929a1 1 0 011.414 0A9.972 9.972 0 0119 10a9.972 9.972 0 01-2.929 7.071 1 1 0 01-1.414-1.414A7.971 7.971 0 0017 10c0-2.21-.894-4.208-2.343-5.657a1 1 0 010-1.414zm-2.829 2.828a1 1 0 011.415 0A5.983 5.983 0 0115 10a5.984 5.984 0 01-1.757 4.243 1 1 0 01-1.415-1.415A3.984 3.984 0 0013 10a3.983 3.983 0 00-1.172-2.828 1 1 0 010-1.415z" clipRule="evenodd" />
                  </svg>
                </div>
                <div>
                  <h3 className="settings-card__title">Text-to-Speech (TTS) Configuration</h3>
                  <p className="settings-card__desc">AI Examiner voice synthesis and question recitation</p>
                </div>
              </div>

              <div className="settings-group">
                <span className="settings-label">AI Examiner Voice Language</span>
                <div className="settings-options-grid">
                  <div
                    className={`lang-picker-option ${settings.ttsLanguage === "vi-VN" ? "is-selected" : ""}`}
                    onClick={() => setSettings((s) => ({ ...s, ttsLanguage: "vi-VN" }))}
                  >
                    <span className="lang-flag">🇻🇳</span>
                    <div className="lang-text">
                      <span className="lang-name">Vietnamese</span>
                      <span className="lang-code">vi-VN</span>
                    </div>
                  </div>

                  <div
                    className={`lang-picker-option ${settings.ttsLanguage === "en-US" ? "is-selected" : ""}`}
                    onClick={() => setSettings((s) => ({ ...s, ttsLanguage: "en-US" }))}
                  >
                    <span className="lang-flag">🇺🇸</span>
                    <div className="lang-text">
                      <span className="lang-name">English</span>
                      <span className="lang-code">en-US</span>
                    </div>
                  </div>
                </div>
              </div>

              <div className="settings-group">
                <label className="settings-label">Speech Rate (Speed)</label>
                <div className="settings-slider-wrap">
                  <input
                    type="range"
                    min="0.5"
                    max="1.5"
                    step="0.05"
                    className="settings-slider"
                    value={settings.ttsSpeedRate}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, ttsSpeedRate: parseFloat(e.target.value) }))
                    }
                  />
                  <span className="settings-slider-val">{settings.ttsSpeedRate}x</span>
                </div>
              </div>

              {/* Live Test Audio Panel */}
              <div className="test-speech-bar">
                <div>
                  <strong style={{ fontSize: "0.85rem", color: "#0f172a", display: "block" }}>
                    Voice Synthesis Preview:
                  </strong>
                  <span className="test-speech-text">
                    {settings.ttsLanguage === "vi-VN"
                      ? '"Xin chào bạn. Đây là giọng đọc mẫu thử nghiệm của Hệ thống Viva Exam AIVES."'
                      : '"Hello. This is a voice test for the AIVES Viva Examination System."'}
                  </span>
                </div>

                <button
                  type="button"
                  className="qc-btn qc-btn--ghost"
                  style={{ minWidth: "110px", flexShrink: 0 }}
                  onClick={handleTestAudio}
                  disabled={isPlayingTest}
                >
                  {isPlayingTest ? "Speaking..." : "🔊 Test Voice"}
                </button>
              </div>
            </div>
          </div>

          <div className="settings-save-bar">
            <button
              type="button"
              className="qc-btn qc-btn--primary"
              disabled={savingSettings}
              onClick={handleSaveSettings}
            >
              {savingSettings ? "Saving..." : "Save System Settings"}
            </button>
          </div>
        </div>
      )}

      {/* MODAL ADD / EDIT USER */}
      {(isAddUserModalOpen || editingUser) && (
        <div className="modal-overlay" onClick={() => { setIsAddUserModalOpen(false); setEditingUser(null); }}>
          <div className="modal-card" style={{ maxWidth: "480px" }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingUser ? "Edit User" : "Add New User"}</h2>
              <button
                type="button"
                className="modal-close"
                onClick={() => { setIsAddUserModalOpen(false); setEditingUser(null); }}
              >
                &times;
              </button>
            </div>

            <form onSubmit={handleSaveUser}>
              <div className="modal-body">
                <div className="modal-field">
                  <label className="modal-label">Full Name *</label>
                  <input
                    type="text"
                    className="modal-input"
                    placeholder="e.g. John Doe"
                    required
                    value={userForm.fullName}
                    onChange={(e) => setUserForm((f) => ({ ...f, fullName: e.target.value }))}
                  />
                </div>

                <div className="modal-field">
                  <label className="modal-label">
                    University / Work Email *
                    {editingUser && (
                      <span style={{ fontSize: "0.75rem", color: "#64748b", fontWeight: "normal", marginLeft: "0.5rem" }}>
                        🔒 (Unique Identifier - Read Only)
                      </span>
                    )}
                  </label>
                  <input
                    type="email"
                    className="modal-input"
                    placeholder="example@fpt.edu.vn"
                    required
                    disabled={Boolean(editingUser)}
                    value={userForm.email}
                    onChange={(e) => setUserForm((f) => ({ ...f, email: e.target.value }))}
                  />
                </div>

                <div className="modal-field">
                  <label className="modal-label">
                    System Role *
                    {editingUser?.role === "Admin" && (
                      <span style={{ fontSize: "0.75rem", color: "#64748b", fontWeight: "normal", marginLeft: "0.5rem" }}>
                        🔒 (Administrator - Role Locked)
                      </span>
                    )}
                  </label>
                  <select
                    className="modal-input"
                    value={userForm.role}
                    disabled={editingUser?.role === "Admin"}
                    onChange={(e) => setUserForm((f) => ({ ...f, role: e.target.value }))}
                  >
                    <option value="Lecturer">Lecturer</option>
                    <option value="Student">Student</option>
                    <option value="Admin">Admin</option>
                  </select>
                </div>

                {!editingUser && (
                  <div className="modal-field">
                    <label className="modal-label">Initial Password</label>
                    <input
                      type="password"
                      className="modal-input"
                      placeholder="Default: 123456"
                      value={userForm.password}
                      onChange={(e) => setUserForm((f) => ({ ...f, password: e.target.value }))}
                    />
                  </div>
                )}
              </div>

              <div className="modal-footer">
                <button
                  type="button"
                  className="qc-btn qc-btn--ghost"
                  onClick={() => { setIsAddUserModalOpen(false); setEditingUser(null); }}
                >
                  Cancel
                </button>
                <button type="submit" className="qc-btn qc-btn--primary">
                  {editingUser ? "Save Changes" : "Create Account"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </Sidebar>
  );
}
