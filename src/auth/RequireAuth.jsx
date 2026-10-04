import { Navigate } from "react-router-dom";
import { getSession } from "./session.js";

// Chặn các trang trong app khi chưa đăng nhập (chỉ là kiểm tra phía giao diện).
export default function RequireAuth({ children }) {
  return getSession() ? children : <Navigate to="/login" replace />;
}