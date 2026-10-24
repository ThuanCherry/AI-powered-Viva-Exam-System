using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Features.Catalog;

public class ClassQueryService(AivesDbContext db)
{
    public async Task<object> ListAsync(Caller caller, int? courseId, CancellationToken ct)
    {
        var teacher = caller.IsLecturer;
        var query = db.AcademicClasses.Where(c => c.IsActive && c.Course!.IsActive && (teacher
            ? c.LecturerId == caller.UserId && c.Course.LecturerId == caller.UserId
            : db.ClassStudents.Any(s => s.ClassId == c.ClassId && s.StudentId == caller.UserId)));
        if (courseId.HasValue) query = query.Where(c => c.CourseId == courseId);
        return await query.OrderBy(c => c.ClassCode).Select(c => new { c.ClassId, c.ClassCode, c.ClassName, c.CourseId,
            c.Course!.CourseCode, c.Course.CourseName,
            studentCount = teacher ? db.ClassStudents.Count(s => s.ClassId == c.ClassId) : (int?)null }).ToListAsync(ct);
    }
}
