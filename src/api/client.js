import { getSession } from "../auth/session.js";
export const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export class ApiError extends Error {
  constructor(message, status = 0) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function request(path, { method = "GET", body, signal, binary = false } = {}) {
  let response;
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers: { ...(body !== undefined && !(body instanceof FormData) ? { "Content-Type": "application/json" } : {}),
        ...(getSession()?.token ? { Authorization: `Bearer ${getSession().token}` } : {}) },
      body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
      signal,
    });
  } catch (error) {
    if (signal?.aborted) throw signal.reason ?? error;
    if (error.name === "AbortError") throw error;
    throw new ApiError("Không kết nối được máy chủ. Vui lòng kiểm tra backend và mạng.");
  }

  if (response.ok && binary) return response.blob();
  const text = await response.text();
  let payload = null;
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }

  // Nhận về trang HTML nghĩa là request không tới backend (thường do thiếu hoặc sai biến trong .env).
  if (response.ok && typeof payload === "string" && /^\s*</.test(payload)) {
    throw new ApiError("Máy chủ trả dữ liệu không hợp lệ. Kiểm tra cấu hình địa chỉ API.");
  }

  // ApiExceptionFilter của backend trả { success: false, message, details } khi lỗi.
  if (!response.ok || payload?.success === false) {
    throw new ApiError(payload?.message ?? (response.status === 401 ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.' : response.status === 403 ? 'Bạn không có quyền thực hiện thao tác này.' : `Yêu cầu không thành công (mã ${response.status}).`), response.status);
  }

  // Chấp nhận cả dạng bọc { success, data } lẫn dạng trả thẳng đối tượng.
  const isWrapped = payload && typeof payload === "object" && "data" in payload;
  return isWrapped ? payload.data : payload;
}

export const api = {
  get: (path, options) => request(path, { ...options, method: "GET" }),
  post: (path, body, options) => request(path, { ...options, method: "POST", body }),
  put: (path, body, options) => request(path, { ...options, method: "PUT", body }),
  delete: (path, options) => request(path, { ...options, method: "DELETE" }),
};
