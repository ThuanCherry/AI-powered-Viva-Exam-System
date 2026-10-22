import { Navigate } from "react-router-dom";
import { getSession } from "./session.js";
import { getDefaultHomeForRole } from '../lib/roles.js';

// Chặn các trang trong app khi chưa đăng nhập (chỉ là kiểm tra phía giao diện).
export default function RequireAuth({ children, roles, allowedRoles }) {
  const session = getSession();
  if (!session?.token) return <Navigate to="/login" replace />;
  const permitted = roles ?? allowedRoles;
  if (permitted?.length && !permitted.some(role => role.toLowerCase() === (session.role || '').toLowerCase())) return <Navigate to={getDefaultHomeForRole(session.role)} replace />;
  return children;
}
