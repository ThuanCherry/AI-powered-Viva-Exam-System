using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Services;
public static class ExamRules
{
    public static ExamCommand WithAutomaticEnd(ExamCommand command, int participantCount)
    {
        try
        {
            return command with { EndsAt = command.StartsAt.AddMinutes((long)command.DurationMinutesPerStudent * Math.Max(1, participantCount)) };
        }
        catch (ArgumentOutOfRangeException) { return command with { EndsAt = command.StartsAt }; }
    }

    public static string? ArrangeSchedule(Exam exam, IReadOnlyList<ExamParticipant> participants)
    {
        if (exam.StartsAt.Year < 2000) return "Hãy chọn ngày và giờ bắt đầu kỳ thi.";
        if (exam.DurationMinutesPerStudent < 1 || exam.DurationMinutesPerStudent > 1440)
            return "Nhập số phút mỗi sinh viên từ 1 đến 1440, ví dụ 15 hoặc 20 phút.";
        DateTime end;
        try { end = exam.StartsAt.AddMinutes((long)exam.DurationMinutesPerStudent * Math.Max(1, participants.Count)); }
        catch (ArgumentOutOfRangeException) { return "Ngày bắt đầu hoặc tổng thời lượng nằm ngoài phạm vi cho phép."; }
        exam.EndsAt = end;
        exam.UpdatedAt = DateTime.UtcNow;
        var cursor = exam.StartsAt;
        foreach (var participant in participants.OrderBy(x => x.SlotOrder))
        {
            participant.ScheduledStartAt = cursor;
            participant.ScheduledEndAt = cursor.AddMinutes(exam.DurationMinutesPerStudent);
            participant.UpdatedAt = DateTime.UtcNow;
            cursor = participant.ScheduledEndAt;
        }
        return null;
    }

    public static string PoolShortage(int selected, int required) =>
        $"Đã chọn {selected} câu, nhưng mỗi sinh viên cần {required} câu chính. Chọn thêm {Math.Max(0, required - selected)} câu đã duyệt, hoặc giảm số câu chính trong cấu hình kỳ thi.";

    public static string? Validate(ExamCommand x)
    {
        if (string.IsNullOrWhiteSpace(x.Title) || x.Title.Trim().Length > 255) return "Tiêu đề cần từ 1 đến 255 ký tự.";
        if (x.CourseId <= 0 || x.StartsAt.Year < 2000 || x.StartsAt >= x.EndsAt) return "Hãy chọn môn học, ngày bắt đầu và số phút mỗi sinh viên hợp lệ.";
        if (x.DurationMinutesPerStudent <= 0 || x.DurationMinutesPerStudent > 1440) return "Thời lượng mỗi sinh viên phải từ 1 đến 1440 phút.";
        if (x.MainQuestionCount <= 0 || x.MainQuestionCount > 1000 || x.MaxFollowUpPerQuestion < 0 || x.MaxFollowUpPerQuestion > 1000)
            return "Số câu chính phải từ 1 đến 1000, câu đào sâu từ 0 đến 1000.";
        if (x.SelectionStrategy is not ("RANDOM" or "ADAPTIVE") || x.AvoidRecentDuplicateCount < 0 || x.AvoidRecentDuplicateCount > 1000)
            return "Chiến lược hoặc số thí sinh cần tránh trùng không hợp lệ.";
        return null;
    }
    public static string? ValidateSchedule(Exam exam, IReadOnlyList<ExamParticipant> participants)
    {
        if (participants.Count == 0) return "Cần ít nhất một thí sinh.";
        if (participants.Select(x => x.StudentId).Distinct().Count() != participants.Count ||
            participants.Select(x => x.SlotOrder).Distinct().Count() != participants.Count)
            return "Danh sách thí sinh hoặc thứ tự bị trùng.";
        var ordered = participants.OrderBy(x => x.ScheduledStartAt).ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            var p = ordered[i];
            if (p.ScheduledStartAt >= p.ScheduledEndAt)
                return $"Lượt thi thứ {p.SlotOrder} chưa có giờ kết thúc sau giờ bắt đầu. Bấm 'Lưu và xếp lịch tự động' để tính lại lịch.";
            if (p.ScheduledStartAt < exam.StartsAt || p.ScheduledEndAt > exam.EndsAt)
                return $"Lượt thi thứ {p.SlotOrder} nằm ngoài khung giờ kỳ thi {exam.StartsAt:dd/MM HH:mm}–{exam.EndsAt:dd/MM HH:mm}. Bấm 'Lưu và xếp lịch tự động' để tính lại toàn bộ lịch.";
            if (i > 0 && ordered[i - 1].ScheduledEndAt > p.ScheduledStartAt) return "Các slot thi không được chồng lấn.";
        }
        return null;
    }
}
