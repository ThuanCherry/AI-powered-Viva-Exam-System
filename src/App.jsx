import { Navigate, Route, Routes } from "react-router-dom";
import Login from "./pages/Login.jsx";
import Register from "./pages/Register.jsx";
import Dashboard from "./pages/Dashboard.jsx";
import QuestionBank from "./pages/QuestionBank.jsx";
import QuestionConfig from "./pages/QuestionConfig.jsx";
import RequireAuth from "./auth/RequireAuth.jsx";

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/dashboard" element={<RequireAuth><Dashboard /></RequireAuth>} />
      <Route path="/questions" element={<RequireAuth><QuestionBank /></RequireAuth>} />
      <Route
        path="/exams/:examId/config"
        element={<RequireAuth><QuestionConfig /></RequireAuth>}
      />
    </Routes>
  );
}

export default App;