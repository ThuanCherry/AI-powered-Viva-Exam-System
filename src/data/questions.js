// Dữ liệu giả dùng chung cho trang cấu hình câu hỏi.
// TODO: thay bằng API (ví dụ GET /api/questions) khi backend sẵn sàng.

export const ALL_SUBJECTS = "All subjects";
export const ALL_DIFFICULTIES = "All difficulty";

export const SUBJECTS = [
  ALL_SUBJECTS,
  "Database Systems",
  "Software Engineering",
  "Network Security",
  "AI & Machine Learning",
];

export const DIFFICULTIES = [ALL_DIFFICULTIES, "Easy", "Medium", "Hard"];

export const QUESTIONS = [
  { id: "q1", text: "Explain the differences between INNER JOIN and LEFT JOIN with an example.", subject: "Database Systems", difficulty: "Medium" },
  { id: "q2", text: "What are the SOLID principles and why do they matter in software design?", subject: "Software Engineering", difficulty: "Hard" },
  { id: "q3", text: "Describe how a SYN flood attack works and one way to mitigate it.", subject: "Network Security", difficulty: "Hard" },
  { id: "q4", text: "What is the bias-variance tradeoff in machine learning?", subject: "AI & Machine Learning", difficulty: "Medium" },
  { id: "q5", text: "Define database normalization and list the first three normal forms.", subject: "Database Systems", difficulty: "Easy" },
  { id: "q6", text: "What is the difference between authentication and authorization?", subject: "Network Security", difficulty: "Easy" },
  { id: "q7", text: "Walk through the steps of a typical Scrum sprint.", subject: "Software Engineering", difficulty: "Easy" },
  { id: "q8", text: "Explain overfitting and describe two ways to reduce it.", subject: "AI & Machine Learning", difficulty: "Medium" },
  { id: "q9", text: "What does ACID mean in a database transaction?", subject: "Database Systems", difficulty: "Easy" },
  { id: "q10", text: "When would you choose a clustered index over a non-clustered index?", subject: "Database Systems", difficulty: "Hard" },
  { id: "q11", text: "Explain the difference between a primary key and a foreign key.", subject: "Database Systems", difficulty: "Easy" },
  { id: "q12", text: "How do transaction isolation levels prevent dirty reads and phantom reads?", subject: "Database Systems", difficulty: "Hard" },
];