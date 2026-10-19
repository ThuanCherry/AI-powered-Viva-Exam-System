import { Navigate, Route, Routes } from "react-router-dom";
import Login from "./pages/Login.jsx";
import Register from "./pages/Register.jsx";
import Dashboard from "./pages/Dashboard.jsx";
import Exams from "./pages/Exams.jsx";
import QuestionBank from "./pages/QuestionBank.jsx";
import QuestionConfig from "./pages/QuestionConfig.jsx";
import Reports from "./pages/Reports.jsx";
import AdminManagement from "./pages/AdminManagement.jsx";
import VivaExam from "./pages/VivaExam.jsx";
import AiInterview from "./pages/AiInterview.jsx";
import RequireAuth, { getDefaultHomeForRole } from "./auth/RequireAuth.jsx";
import { getSession } from "./auth/session.js";

function RootRedirect() {
  const session = getSession();
  if (!session) return <Navigate to="/login" replace />;
  return <Navigate to={getDefaultHomeForRole(session.role)} replace />;
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />

      {/* ---------- Lecturer-only Routes (Dashboard, Question Bank, Exam Management, Reports) ---------- */}
      <Route
        path="/dashboard"
        element={
          <RequireAuth allowedRoles={["Lecturer"]}>
            <Dashboard />
          </RequireAuth>
        }
      />
      <Route
        path="/exams"
        element={
          <RequireAuth allowedRoles={["Lecturer"]}>
            <Exams />
          </RequireAuth>
        }
      />
      <Route
        path="/exams/:examId/config"
        element={
          <RequireAuth allowedRoles={["Lecturer"]}>
            <QuestionConfig />
          </RequireAuth>
        }
      />
      <Route
        path="/questions"
        element={
          <RequireAuth allowedRoles={["Lecturer"]}>
            <QuestionBank />
          </RequireAuth>
        }
      />
      <Route
        path="/reports"
        element={
          <RequireAuth allowedRoles={["Lecturer"]}>
            <Reports />
          </RequireAuth>
        }
      />

      {/* ---------- Admin-only Route ---------- */}
      <Route
        path="/admin"
        element={
          <RequireAuth allowedRoles={["Admin"]}>
            <AdminManagement />
          </RequireAuth>
        }
      />

      {/* ---------- Student-only Routes (Viva Exam, AI Interview) ---------- */}
      <Route
        path="/viva"
        element={
          <RequireAuth allowedRoles={["Student"]}>
            <VivaExam />
          </RequireAuth>
        }
      />
      <Route
        path="/ai-interview"
        element={
          <RequireAuth allowedRoles={["Student"]}>
            <AiInterview />
          </RequireAuth>
        }
      />

      {/* Catch-all */}
      <Route path="*" element={<RootRedirect />} />
    </Routes>
  );
}

export default App;