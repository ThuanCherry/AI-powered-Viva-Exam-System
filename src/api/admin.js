import { api } from './client.js';

// Failures propagate to the UI. No local records or success fallback.
export const getAdminUsers = () => api.get('/api/Users');
export const createAdminUser = data => api.post('/api/Admin/users', data);
export const updateAdminUser = (id, data) => api.put(`/api/Users/${id}`, data);
export const toggleUserStatus = id => api.put(`/api/Users/${id}/status`, {});
export const getAdminCourses = () => api.get('/api/Courses');
export const assignLecturerToCourse = (id, lecturerId) => api.put(`/api/Courses/${id}/assign-lecturer`, { lecturerId });
export const getSpeechSettings = () => api.get('/api/Admin/settings');
