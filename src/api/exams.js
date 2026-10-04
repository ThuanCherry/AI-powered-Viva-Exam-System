import { api } from "./client.js";

// GET /api/Exams/{id}
export function getExam(examId, signal) {
  return api.get(`/api/Exams/${encodeURIComponent(examId)}`, { signal });
}