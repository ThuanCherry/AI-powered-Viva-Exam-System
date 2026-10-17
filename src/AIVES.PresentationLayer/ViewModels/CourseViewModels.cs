using System.ComponentModel.DataAnnotations;
using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;
namespace AIVES.PresentationLayer.ViewModels;
public class CourseViewModel
{
    [Required, StringLength(50)] public string Code { get; set; } = "";
    [Required, StringLength(255)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public CourseCommand ToCommand() => new(Code, Name, Description, IsActive);
    public static CourseViewModel From(Course c) => new() { Code = c.CourseCode, Name = c.CourseName, Description = c.Description, IsActive = c.IsActive };
}
public class QuestionViewModel
{
    [Required, StringLength(15000)] public string Content { get; set; } = "";
    [RegularExpression("^(Easy|Medium|Hard)?$")] public string? Difficulty { get; set; } = "Medium";
    [StringLength(30)] public string? BloomLevel { get; set; } = "Understand";
    public bool IsApproved { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public QuestionCommand ToCommand() => new(Content, Difficulty, BloomLevel, IsApproved, IsActive);
    public static QuestionViewModel From(Question q) => new() { Content = q.QuestionText, Difficulty = q.Difficulty, BloomLevel = q.BloomLevel, IsApproved = q.IsApproved, IsActive = q.IsActive };
}