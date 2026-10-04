// Lưu thông tin người dùng sau khi đăng nhập.
// Backend hiện chưa phát token (OpenAPI không có JWT), nên đây chỉ là thông tin hiển thị, không phải bảo mật thật.
const KEY = "aives.session";

function stores() {
  try {
    return [window.localStorage, window.sessionStorage];
  } catch {
    return [];
  }
}

export function saveSession(user, remember) {
  clearSession();
  try {
    const [local, session] = stores();
    (remember ? local : session)?.setItem(KEY, JSON.stringify(user));
  } catch {
    // Trình duyệt chặn storage: bỏ qua, người dùng sẽ phải đăng nhập lại khi tải lại trang.
  }
}

export function getSession() {
  for (const store of stores()) {
    try {
      const raw = store.getItem(KEY);
      if (raw) return JSON.parse(raw);
    } catch {
      // dữ liệu hỏng: bỏ qua
    }
  }
  return null;
}

export function clearSession() {
  for (const store of stores()) {
    try {
      store.removeItem(KEY);
    } catch {
      // bỏ qua
    }
  }
}