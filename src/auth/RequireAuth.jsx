import { Navigate } from "react-router-dom";
import { getSession } from "./session.js";

// Chặn các trang trong app khi chưa đăng nhập (chỉ là kiểm tra phía giao diện).
export default function RequireAuth({ children, roles }) {
  const session = getSession();
  if (!session?.token) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(session.role)) return <Navigate to="/exams" replace />;
  return children;
}
