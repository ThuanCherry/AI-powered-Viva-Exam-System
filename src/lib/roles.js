export function getDefaultHomeForRole(role) {
  switch ((role || '').toLowerCase()) {
    case 'admin': return '/admin';
    case 'lecturer': return '/dashboard';
    case 'student': return '/exams';
    default: return '/login';
  }
}
