using System.ComponentModel.DataAnnotations;
using AIVES.DataAccessLayer.Models;

namespace AIVES.PresentationLayer.ViewModels;

public class ExamListViewModel
{
    public List<Exam> Exams { get; set; } = new();
    public string? SelectedSubject { get; set; }
    public string? SearchTerm { get; set; }
}

public class CreateExamViewModel
{
    [Required(ErrorMessage = "Tiêu đề bài thi không được để trống")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Môn học không được để trống")]
    public string Subject { get; set; } = string.Empty;

    public int? CourseId { get; set; }

    [Range(5, 180, ErrorMessage = "Thời gian từ 5 đến 180 phút")]
    public int DurationMinutes { get; set; } = 30;

    public string Difficulty { get; set; } = "Medium";
}

public class VivaSessionViewModel
{
    public int SessionId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartTime { get; set; }
    public List<Question> Questions { get; set; } = new();
    public int CurrentQuestionIndex { get; set; }
}
