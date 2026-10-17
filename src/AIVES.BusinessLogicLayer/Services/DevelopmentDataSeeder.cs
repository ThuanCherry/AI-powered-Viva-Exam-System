using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
namespace AIVES.BusinessLogicLayer.Services;
public class DevelopmentDataSeeder(IUnitOfWork uow, IPasswordHasher<User> hasher, IHostEnvironment environment,
    IExamService exams, IExamSessionService schedules, IQuestionAssignmentService assignments)
{
    public async Task SeedAsync()
    {
        if (!environment.IsDevelopment()) return;
        foreach (var code in new[] { "ADMIN", "LECTURER", "STUDENT" })
        {
            if (!await uow.Roles.ExistsAsync(x => x.Code == code))
            {
                await uow.Roles.AddAsync(new Role { Code = code, Name = code });
                await uow.SaveChangesAsync();
            }
        }
        var lecturer1 = await Account("lecturer1@aives.local", "Lecturer 1", "LECTURER", "GV001");
        var lecturer2 = await Account("lecturer2@aives.local", "Lecturer 2", "LECTURER", "GV002");
        var students = new List<User>();
        for (int i = 1; i <= 8; i++)
            students.Add(await Account($"student{i:00}@aives.local", $"Student {i:00}", "STUDENT", $"SV{i:000}"));
        var prn = await Course("PRN222", "Advanced Programming with .NET", lecturer1, students);
        await Course("SWP391", "Software Development Project", lecturer2, students.Take(5).ToArray());
        string[] content =
        {
            "Dependency Injection trong ASP.NET Core là gì?", "Phân biệt Scoped, Transient và Singleton.",
            "DbContext nên có lifetime thế nào?", "Repository Pattern giải quyết vấn đề gì?",
            "Unit of Work có vai trò gì?", "Middleware Pipeline hoạt động thế nào?",
            "Authentication khác Authorization thế nào?", "async/await nên dùng khi nào?",
            "LINQ deferred execution là gì?", "EF Core Tracking và AsNoTracking khác nhau thế nào?",
            "Làm sao hạn chế SQL Injection khi dùng EF Core?", "Dependency Inversion trong SOLID là gì?"
        };
        for (var i = 0; i < content.Length; i++)
        {
            var text = content[i];
            if (!await uow.Questions.ExistsAsync(x => x.CourseId == prn.CourseId && x.QuestionText == text))
                await uow.Questions.AddAsync(new Question { CourseId = prn.CourseId, QuestionText = text, IsApproved = true,
                    Difficulty = new[] { "Easy", "Medium", "Hard" }[i % 3], BloomLevel = new[] { "Remember", "Understand", "Apply" }[i % 3] });
        }
        await uow.SaveChangesAsync();
        await DemoExam(prn, lecturer1, students, "PRN222 Viva - Draft Demo", false);
        await DemoExam(prn, lecturer1, students.Take(5).ToArray(), "PRN222 Viva - Published Demo", true);
    }
    private async Task<User> Account(string email, string name, string roleCode, string code)
    {
        var user = (await uow.Users.FindAsync(x => x.Email == email)).SingleOrDefault();
        if (user == null)
        {
            user = new User { Email = email, FullName = name, StudentCode = roleCode == "STUDENT" ? code : null, LecturerCode = roleCode == "LECTURER" ? code : null };
            user.PasswordHash = hasher.HashPassword(user, "Demo@123");
            await uow.Users.AddAsync(user);
            await uow.SaveChangesAsync();
        }
        var role = (await uow.Roles.FindAsync(x => x.Code == roleCode)).Single();
        if (!await uow.UserRoles.ExistsAsync(x => x.UserId == user.UserId && x.RoleId == role.RoleId))
        {
            await uow.UserRoles.AddAsync(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
            await uow.SaveChangesAsync();
        }
        return user;
    }
    private async Task<Course> Course(string code, string name, User lecturer, IReadOnlyCollection<User> students)
    {
        var course = (await uow.Courses.FindAsync(x => x.CourseCode == code)).SingleOrDefault();
        if (course == null)
        {
            course = new Course { CourseCode = code, CourseName = name };
            await uow.Courses.AddAsync(course);
            await uow.SaveChangesAsync();
        }
        if (!await uow.CourseLecturers.ExistsAsync(x => x.CourseId == course.CourseId && x.LecturerId == lecturer.UserId))
            await uow.CourseLecturers.AddAsync(new CourseLecturer { CourseId = course.CourseId, LecturerId = lecturer.UserId });
        foreach (var student in students)
            if (!await uow.CourseStudents.ExistsAsync(x => x.CourseId == course.CourseId && x.StudentId == student.UserId))
                await uow.CourseStudents.AddAsync(new CourseStudent { CourseId = course.CourseId, StudentId = student.UserId });
        await uow.SaveChangesAsync();
        return course;
    }
    private async Task DemoExam(Course course, User lecturer, IReadOnlyCollection<User> students, string title, bool publish)
    {
        // Existing demo exams (including user edits) are never regenerated on restart.
        if (await uow.Exams.ExistsAsync(x => x.Title == title && x.CreatedByUserId == lecturer.UserId)) return;
        var start = DateTime.Today.AddDays(1).AddHours(publish ? 13 : 8);
        var created = await exams.CreateAsync(new(course.CourseId, title, "Development demo", start, start.AddHours(3), 15, 3, 2, "RANDOM", 1), lecturer.UserId);
        if (!created.Success) throw new InvalidOperationException(created.Message);
        if (!publish) return;
        var id = created.Data!.ExamId;
        var participantResult = await exams.SetParticipantsAsync(id, students.Select(x => x.UserId).ToArray(), lecturer.UserId);
        var scheduleResult = await schedules.GenerateScheduleAsync(id, lecturer.UserId);
        var poolIds = (await uow.Questions.FindAsync(x => x.CourseId == course.CourseId && x.IsActive && x.IsApproved)).Select(x => x.QuestionId).ToArray();
        var poolResult = await exams.SetQuestionPoolAsync(id, poolIds, lecturer.UserId);
        var assignmentResult = await assignments.GenerateAsync(id, lecturer.UserId);
        var published = await exams.PublishAsync(id, lecturer.UserId);
        foreach (var result in new[] { participantResult, scheduleResult, poolResult, assignmentResult, published })
            if (!result.Success) throw new InvalidOperationException(result.Message);
    }
}