// Địa chỉ backend lấy từ file .env, ví dụ: VITE_API_BASE_URL=http://localhost:5234
const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export class ApiError extends Error {
  constructor(message, status = 0) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function request(path, { method = "GET", body, signal } = {}) {
  let response;
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers: body === undefined ? undefined : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  } catch (error) {
    if (error.name === "AbortError") throw error;
    throw new ApiError("Cannot reach the server. Check that the backend is running.");
  }

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
    throw new ApiError("Unexpected response from the server. Check VITE_API_BASE_URL in your .env file.");
  }

  // ApiExceptionFilter của backend trả { success: false, message, details } khi lỗi.
  if (!response.ok || payload?.success === false) {
    throw new ApiError(payload?.message ?? `Request failed (${response.status}).`, response.status);
  }

  // Chấp nhận cả dạng bọc { success, data } lẫn dạng trả thẳng đối tượng.
  const isWrapped = payload && typeof payload === "object" && "data" in payload;
  return isWrapped ? payload.data : payload;
}

export const api = {
  get: (path, options) => request(path, { ...options, method: "GET" }),
  post: (path, body, options) => request(path, { ...options, method: "POST", body }),
};