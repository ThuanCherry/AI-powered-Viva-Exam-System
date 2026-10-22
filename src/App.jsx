import { Navigate, Route, Routes } from "react-router-dom";
import Login from "./pages/Login.jsx";
import Register from "./pages/Register.jsx";
import LecturerDashboard from "./pages/LecturerDashboard.jsx";
import { CoursesPage, ClassesPage, ClassWorkspace } from "./pages/Teaching.jsx";
import { DocumentsPage, ManualQuestionsPage } from "./pages/TeachingResources.jsx";
import RequireAuth from "./auth/RequireAuth.jsx";
import Exams, { ExamEditor } from "./pages/Exams.jsx";
import VivaRoom from "./pages/VivaRoom.jsx";
import AdminManagement from './pages/AdminManagement.jsx';
import { getDefaultHomeForRole } from './lib/roles.js';
import { getSession } from './auth/session.js';

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to={getDefaultHomeForRole(getSession()?.role)} replace />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/dashboard" element={<RequireAuth roles={['Lecturer']}><LecturerDashboard /></RequireAuth>} />
      <Route path="/admin" element={<RequireAuth roles={['Admin']}><AdminManagement /></RequireAuth>} />
      <Route path="/courses" element={<RequireAuth roles={['Lecturer']}><CoursesPage /></RequireAuth>} />
      <Route path="/classes" element={<RequireAuth><ClassesPage /></RequireAuth>} />
      <Route path="/classes/:classId" element={<RequireAuth roles={['Lecturer']}><ClassWorkspace /></RequireAuth>} />
      <Route path="/documents" element={<RequireAuth roles={['Lecturer']}><DocumentsPage /></RequireAuth>} />
      <Route path="/questions" element={<RequireAuth roles={['Lecturer']}><ManualQuestionsPage /></RequireAuth>} />
      <Route path="/exams" element={<RequireAuth roles={['Lecturer', 'Student']}><Exams /></RequireAuth>} />
      <Route path="/exams/new" element={<RequireAuth roles={['Lecturer']}><ExamEditor /></RequireAuth>} />
      <Route path="/exams/:examId" element={<RequireAuth roles={['Lecturer']}><ExamEditor /></RequireAuth>} />
      <Route path="/viva" element={<Navigate to="/exams" replace />} />
      <Route path="/viva/:examId" element={<RequireAuth roles={['Student']}><VivaRoom /></RequireAuth>} />
      <Route
        path="/exams/:examId/config"
        element={<RequireAuth roles={['Lecturer']}><ExamEditor /></RequireAuth>}
      />
      <Route path="*" element={<Navigate to={getDefaultHomeForRole(getSession()?.role)} replace />} />
    </Routes>
  );
}

export default App;
