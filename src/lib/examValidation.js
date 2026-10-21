export function examSaveIssues(form, poolSize, requireClass = true) {
  const issues = [];
  if (!form.courseId) issues.push('Chọn môn học.');
  if (requireClass && !form.classId) issues.push('Chọn lớp học phần.');
  if (!form.examName?.trim()) issues.push('Nhập tên kỳ thi.');
  if (!form.startTime || !Number.isFinite(new Date(`${form.startTime}:00+07:00`).getTime())) issues.push('Chọn ngày và giờ bắt đầu thi.');
  if (!Number.isInteger(form.timePerStudent) || form.timePerStudent < 1 || form.timePerStudent > 180) issues.push('Thời lượng mỗi sinh viên cần từ 1–180 phút.');
  if (!form.studentIds?.length) issues.push('Chọn ít nhất một sinh viên trong danh sách lớp.');
  if (!Number.isInteger(form.mainQuestionCount) || form.mainQuestionCount < 1 || form.mainQuestionCount > 20) issues.push('Số câu chính cần từ 1–20.');
  if (form.courseId && poolSize == null) issues.push('Chưa tải được số câu hỏi. Kiểm tra kết nối máy chủ.');
  else if (poolSize < form.mainQuestionCount) issues.push(`Chỉ có ${poolSize} câu Sẵn sàng, đang yêu cầu ${form.mainQuestionCount}. Giảm số câu chính hoặc bổ sung câu hỏi.`);
  return issues;
}
