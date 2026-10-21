import { api } from "./client.js";

// GET /api/Exams
export function getExams(signal) {
  return api.get("/api/Exams", { signal });
}

// GET /api/Exams/{id}
export function getExam(examId, signal) {
  return api.get(`/api/Exams/${encodeURIComponent(examId)}`, { signal });
}

// POST /api/Exams
export function createExam(payload) {
  return api.post("/api/Exams", payload);
}

// POST /api/Exams/{id}/candidates
export function assignCandidates(examId, candidates) {
  return api.post(`/api/Exams/${encodeURIComponent(examId)}/candidates`, { candidates });
}

// POST /api/Exams/{id}/generate-questions
export function generateExamQuestions(examId, payload) {
  return api.post(`/api/Exams/${encodeURIComponent(examId)}/generate-questions`, payload);
}

// PUT /api/Exams/{id}/status
export function updateExamStatus(examId, status) {
  return api.put(`/api/Exams/${encodeURIComponent(examId)}/status`, { status });
}