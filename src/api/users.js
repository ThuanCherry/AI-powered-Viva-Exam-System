import { api } from "./client.js";

// OpenAPI không mô tả response, nên đọc linh hoạt: có thể là { user: {...} } hoặc chính user.
function toUser(payload, fallbackEmail) {
  const raw = payload && typeof payload === "object" ? (payload.user ?? payload) : {};
  return {
    userId: raw.userId ?? raw.id ?? null,
    fullName: raw.fullName ?? raw.name ?? null,
    email: raw.email ?? fallbackEmail,
    role: raw.role ?? null,
  };
}

// POST /api/Users/login  { email, password, rememberMe }
export async function login({ email, password, rememberMe }) {
  const payload = await api.post("/api/Users/login", { email, password, rememberMe });
  return toUser(payload, email);
}

// POST /api/Users/register  { fullName, email, password, confirmPassword, role? }
export function register({ fullName, email, password, confirmPassword }) {
  return api.post("/api/Users/register", { fullName, email, password, confirmPassword });
}