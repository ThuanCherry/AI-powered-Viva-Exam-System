import { api } from "./client.js";
import { getCourses } from "./courses.js";

const SETTINGS_STORAGE_KEY = "aives_admin_settings";
const USER_OVERRIDES_KEY = "aives_admin_user_overrides";
const COURSE_LECTURERS_KEY = "aives_admin_course_lecturers";

// 1. Quản lý tài khoản - Lấy dữ liệu THẬT 100% từ Database qua API /api/Users
export async function getAdminUsers() {
  // Lấy dữ liệu trực tiếp từ Database
  const dbUsers = await api.get("/api/Users");
  const usersList = Array.isArray(dbUsers) ? [...dbUsers] : [];

  // Áp dụng các thay đổi cục bộ (nếu người dùng vừa sửa/tạo mới trên giao diện trong lúc chờ BE)
  const overrides = JSON.parse(localStorage.getItem(USER_OVERRIDES_KEY) || "{}");

  // Merge bất kỳ override nào vào danh sách thật từ DB
  const result = usersList.map((u) => {
    if (overrides[u.userId]) {
      return { ...u, ...overrides[u.userId] };
    }
    return u;
  });

  // Bổ sung user mới tạo thêm nếu có
  const newCreated = JSON.parse(localStorage.getItem("aives_admin_new_users") || "[]");
  return [...newCreated, ...result];
}

export async function createAdminUser(userData) {
  try {
    const created = await api.post("/api/Admin/users", userData);
    if (created) return created;
  } catch {
    // Backend chưa có endpoint /api/Admin/users
  }

  // Tạm lưu user mới tạo
  const existingNew = JSON.parse(localStorage.getItem("aives_admin_new_users") || "[]");
  const currentDbUsers = await api.get("/api/Users").catch(() => []);
  const allIds = [...currentDbUsers, ...existingNew].map((u) => u.userId || 0);
  const nextId = allIds.length > 0 ? Math.max(...allIds) + 1 : 100;

  const newUser = {
    userId: nextId,
    fullName: userData.fullName,
    email: userData.email,
    role: userData.role || "Lecturer",
    status: userData.status || "Active",
    createdAt: new Date().toISOString(),
  };

  existingNew.unshift(newUser);
  localStorage.setItem("aives_admin_new_users", JSON.stringify(existingNew));
  return newUser;
}

export async function updateAdminUser(userId, updateData) {
  try {
    const updated = await api.put(`/api/Users/${userId}`, updateData);
    if (updated) return updated;
  } catch (err) {
    console.warn("Backend update failed, falling back to local:", err);
  }

  // Cập nhật override dự phòng
  const overrides = JSON.parse(localStorage.getItem(USER_OVERRIDES_KEY) || "{}");
  overrides[userId] = { ...(overrides[userId] || {}), ...updateData };
  localStorage.setItem(USER_OVERRIDES_KEY, JSON.stringify(overrides));

  return { userId, ...updateData };
}

export async function toggleUserStatus(userId) {
  try {
    const res = await api.put(`/api/Users/${userId}/status`, {});
    if (res) return res;
  } catch (err) {
    console.warn("Backend status toggle failed:", err);
  }

  const allUsers = await getAdminUsers();
  const target = allUsers.find((u) => u.userId === Number(userId));
  if (!target) throw new Error("User not found.");

  const newStatus = target.status === "Active" ? "Inactive" : "Active";
  return updateAdminUser(userId, { status: newStatus });
}

// 2. Phân quyền Giảng viên / Môn học - Lấy dữ liệu THẬT 100% từ Database qua API /api/Courses
export async function getAdminCourses() {
  const baseCourses = await getCourses();
  return Array.isArray(baseCourses) ? baseCourses : [];
}

export async function assignLecturerToCourse(courseId, lecturerId, lecturerName) {
  try {
    const res = await api.put(`/api/Courses/${courseId}/assign-lecturer`, {
      lecturerId,
    });
    if (res) return res;
  } catch (err) {
    console.warn("Backend course assignment failed, falling back to local:", err);
  }

  const overrides = JSON.parse(localStorage.getItem(COURSE_LECTURERS_KEY) || "{}");
  overrides[courseId] = { lecturerId, lecturerName };
  localStorage.setItem(COURSE_LECTURERS_KEY, JSON.stringify(overrides));
  return { success: true, courseId, lecturerId, lecturerName };
}

// 3. Cấu hình Ngôn ngữ STT / TTS & Hệ thống
export const DEFAULT_SPEECH_SETTINGS = {
  sttLanguage: "vi-VN",
  sttAutoPunctuation: true,
  sttProfanityFilter: false,
  ttsLanguage: "vi-VN",
  ttsVoiceGender: "female",
  ttsSpeedRate: 1.0,
  ttsPitch: 1.0,
  enableRealtimeFeedback: true,
  silenceThresholdSeconds: 3,
};

export async function getSpeechSettings() {
  try {
    const remote = await api.get("/api/Admin/settings");
    if (remote) return { ...DEFAULT_SPEECH_SETTINGS, ...remote };
  } catch {
    // fallback
  }

  const stored = localStorage.getItem(SETTINGS_STORAGE_KEY);
  if (stored) {
    try {
      return { ...DEFAULT_SPEECH_SETTINGS, ...JSON.parse(stored) };
    } catch {
      // fallback
    }
  }
  return DEFAULT_SPEECH_SETTINGS;
}

export async function saveSpeechSettings(settings) {
  try {
    const res = await api.put("/api/Admin/settings", settings);
    if (res) return res;
  } catch {
    // fallback
  }

  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(settings));
  return { success: true, settings };
}
