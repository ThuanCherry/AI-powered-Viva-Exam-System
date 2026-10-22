import { api } from "./client.js";

// GET /api/Questions
export function getQuestions({ courseId, difficulty, status, bloomLevel } = {}, signal) {
  const params = new URLSearchParams();
  if (courseId !== undefined && courseId !== null) params.append("courseId", courseId);
  if (difficulty) params.append("difficulty", difficulty);
  if (status) params.append("status", status);
  if (bloomLevel) params.append("bloomLevel", bloomLevel);

  const queryString = params.toString();
  const url = queryString ? `/api/Questions?${queryString}` : "/api/Questions";
  return api.get(url, { signal });
}

// GET /api/Questions/{id}
export function getQuestion(id, signal) {
  return api.get(`/api/Questions/${encodeURIComponent(id)}`, { signal });
}
