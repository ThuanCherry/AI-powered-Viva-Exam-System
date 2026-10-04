import { Navigate, Route, Routes } from "react-router-dom";
import Login from "./pages/Login.jsx";
import Dashboard from "./pages/Dashboard.jsx";
import QuestionBank from "./pages/QuestionBank.jsx";
import QuestionConfig from "./pages/QuestionConfig.jsx";

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<Login />} />
      <Route path="/dashboard" element={<Dashboard />} />
      <Route path="/questions" element={<QuestionBank />} />
      <Route path="/exams/:examId/config" element={<QuestionConfig />} />
    </Routes>
  );
}

export default App;