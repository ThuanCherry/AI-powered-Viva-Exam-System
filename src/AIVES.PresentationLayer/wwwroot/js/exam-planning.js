document.querySelectorAll("[data-delete-exam]").forEach(form => {
    form.addEventListener("submit", event => {
        if (!window.confirm(`Xóa kỳ thi “${form.dataset.examTitle}”? Lịch thi và bộ câu hỏi đã gán sẽ bị xóa. Thao tác này không thể hoàn tác.`)) {
            event.preventDefault();
        }
    });
});

document.querySelectorAll("[data-pool-form]").forEach(form => {
    const checkboxes = Array.from(form.querySelectorAll('input[name="QuestionIds"]'));
    const output = form.querySelector("[data-pool-count]");
    const required = Number(form.dataset.required);
    const update = () => {
        const count = checkboxes.filter(input => input.checked).length;
        if (output) output.textContent = count < required
            ? `Đang chọn ${count}/${required} câu tối thiểu. Chọn thêm ${required - count} câu trước khi tạo bộ đề.`
            : `Đang chọn ${count} câu; đủ cho ${required} câu mỗi sinh viên. Bấm Lưu để áp dụng lựa chọn.`;
    };
    checkboxes.forEach(input => input.addEventListener("change", update));
    form.querySelector("[data-select-all]")?.addEventListener("click", () => {
        checkboxes.filter(input => !input.disabled).forEach(input => input.checked = true);
        update();
    });
    update();
});
document.querySelectorAll("[data-schedule-form]").forEach(form => {
    const start = form.querySelector('[name="StartsAt"]');
    const minutes = form.querySelector('[name="DurationMinutesPerStudent"]');
    const output = form.querySelector("[data-schedule-end]");
    const update = () => {
        const count = Number(form.dataset.count);
        const date = new Date(start.value);
        if (count < 1) { output.textContent = "Chọn thí sinh trước để tính giờ kết thúc"; return; }
        const value = Number(minutes.value);
        if (!Number.isFinite(date.getTime()) || value < 1 || value > 1440) { output.textContent = "Nhập giờ bắt đầu và số phút hợp lệ"; return; }
        const end = new Date(date.getTime() + count * value * 60000);
        output.textContent = end.toLocaleString("vi-VN");
    };
    start.addEventListener("input", update);
    minutes.addEventListener("input", update);
    update();
});
