using AIVES.DataAccessLayer.Data;
using AIVES.DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AIVES.Tests;
public class SchemaMappingTests
{
    [Fact]
    public void Model_UsesExistingFunction2TablesAndColumns()
    {
        using var context = new AivesDbContext(new DbContextOptionsBuilder<AivesDbContext>().UseMySQL("Server=localhost;Database=aives_swd;User=root;").Options);
        var model = context.Model;
        Assert.Equal("users", model.FindEntityType(typeof(User))!.GetTableName());
        Assert.Equal("exams", model.FindEntityType(typeof(Exam))!.GetTableName());
        Assert.Equal("exam_participants", model.FindEntityType(typeof(ExamParticipant))!.GetTableName());
        Assert.Equal("exam_question_assignments", model.FindEntityType(typeof(ExamQuestionAssignment))!.GetTableName());
        Assert.Null(model.FindEntityType(typeof(ExamSession)));
        Assert.Null(model.FindEntityType(typeof(StudentAnswer)));
        Assert.Equal(typeof(long), model.FindEntityType(typeof(User))!.FindProperty(nameof(User.UserId))!.ClrType);
        var table = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table("exams", null);
        Assert.Equal("course_id", model.FindEntityType(typeof(Exam))!.FindProperty(nameof(Exam.CourseId))!.GetColumnName(table));
        Assert.Equal("created_by", model.FindEntityType(typeof(Exam))!.FindProperty(nameof(Exam.CreatedByUserId))!.GetColumnName(table));
    }
}