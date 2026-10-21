import { api } from "./client.js";

// GET /api/Courses
export function getCourses(signal) {
  return api.get("/api/Courses", { signal });
}

// GET /api/Courses/{id}
export function getCourse(courseId, signal) {
  return api.get(`/api/Courses/${encodeURIComponent(courseId)}`, { signal });
}
