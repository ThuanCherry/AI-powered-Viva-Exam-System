import { Navigate } from "react-router-dom";
import { getSession } from "./session.js";

export function getDefaultHomeForRole(role) {
  const norm = (role || "").toLowerCase();
  if (norm === "admin") return "/admin";
  if (norm === "lecturer") return "/dashboard";
  if (norm === "student") return "/viva";
  return "/login";
}

// Chặn các trang trong app khi chưa đăng nhập hoặc không đúng quyền (Role-based access control)
export default function RequireAuth({ children, allowedRoles }) {
  const session = getSession();
  if (!session) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && allowedRoles.length > 0) {
    const userRole = (session.role || "").toLowerCase();
    const hasRole = allowedRoles.some((r) => r.toLowerCase() === userRole);
    if (!hasRole) {
      // Chuyển hướng người dùng về đúng trang chủ thuộc quyền của họ
      const defaultHome = getDefaultHomeForRole(userRole);
      return <Navigate to={defaultHome} replace />;
    }
  }

  return children;
}