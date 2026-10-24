using System.IO.Compression;
using System.Text.RegularExpressions;
using AIVES.Api.Data;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Lecturer;

public class DocumentStore(IConfiguration config, IWebHostEnvironment env, AivesDbContext db, LecturerService lecturer)
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private string Root => Path.GetFullPath(config["Documents:StorageRoot"] ?? Path.Combine(env.ContentRootPath, "App_Data", "Documents"));
    public async Task<int> Upload(int course, int user, IFormFile file, CancellationToken ct)
    {
        await lecturer.Course(course, user, ct);
        Require(file.Length is > 0 and <= MaxBytes, "Tài liệu cần từ 1 byte đến 10MB.", 413);
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        Require(name.Length is > 0 and <= 255 && !name.Any(char.IsControl), "Tên file không hợp lệ.");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        Require(extension is ".pdf" or ".docx", "Chỉ nhận PDF hoặc DOCX.", 415);
        using var buffer = new MemoryStream(); await file.CopyToAsync(buffer, ct);
        Require(buffer.Length <= MaxBytes, "File vượt giới hạn 10MB.", 413);
        var bytes = buffer.ToArray();
        if (extension == ".pdf") Require(bytes.Length >= 5 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-", "Nội dung không phải PDF.", 415);
        else
        {
            try
            {
                using var zip = new ZipArchive(buffer, ZipArchiveMode.Read, true);
                Require(zip.Entries.Count <= 2000 && zip.Entries.Sum(e => e.Length) <= 50 * 1024 * 1024
                    && zip.GetEntry("[Content_Types].xml") != null && zip.GetEntry("word/document.xml") != null, "DOCX không hợp lệ hoặc vượt giới hạn giải nén.", 415);
            }
            catch (InvalidDataException) { throw new Workflow.WorkflowException(415, "Nội dung không phải DOCX hợp lệ."); }
        }
        Directory.CreateDirectory(Root);
        var storage = Guid.NewGuid().ToString("N") + extension;
        await File.WriteAllBytesAsync(Path.Combine(Root, storage), bytes, ct);
        var doc = new Document { CourseId = course, UploadedById = user, FileName = name, ContentType = extension == ".pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSize = bytes.Length, StoragePath = storage, Status = "Uploaded", CreatedAt = DateTime.UtcNow };
        db.Documents.Add(doc); await db.SaveChangesAsync(ct); return doc.DocumentId;
    }
    public async Task<Document> Accessible(int id, int user, bool isLecturer, CancellationToken ct)
    {
        var doc = await db.Documents.Include(d => d.Course).FirstOrDefaultAsync(d => d.DocumentId == id && d.Status != "Archived", ct);
        Require(doc != null, "Tài liệu không tồn tại hoặc đã được gỡ.", 404);
        var allowed = isLecturer ? doc.Course!.LecturerId == user : await db.ClassStudents.AnyAsync(s => s.StudentId == user && s.AcademicClass!.CourseId == doc.CourseId && s.AcademicClass.IsActive, ct);
        Require(allowed, "Không có quyền tải tài liệu này.", 403); return doc;
    }
    public string FilePath(Document doc)
    {
        Require(Regex.IsMatch(doc.StoragePath, "^[a-f0-9]{32}\\.(pdf|docx)$"), "Tài liệu legacy chưa thuộc kho lưu trữ riêng này.", 404);
        var path = Path.GetFullPath(Path.Combine(Root, doc.StoragePath));
        Require(path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path), "Không tìm thấy file trong kho lưu trữ.", 404);
        return path;
    }
    public async Task Archive(int id, int user, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct); Require(doc != null, "Không tìm thấy tài liệu.", 404);
        await lecturer.Course(doc.CourseId, user, ct); doc.Status = "Archived"; await db.SaveChangesAsync(ct);
        // Keep private bytes and metadata; no physical delete, no AI processing.
    }

    public async Task<object> ListAsync(int courseId, int user, bool isLecturer, CancellationToken ct)
    {
        if (isLecturer) await lecturer.Course(courseId, user, ct);
        else Require(await db.ClassStudents.AnyAsync(s => s.StudentId == user && s.AcademicClass!.CourseId == courseId && s.AcademicClass.IsActive, ct), "Bạn chưa ghi danh môn học này.", 403);
        return await db.Documents.Where(d => d.CourseId == courseId && d.Status != "Archived").OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.DocumentId, d.CourseId, d.FileName, d.ContentType, d.FileSize, d.CreatedAt, d.Status }).ToListAsync(ct);
    }
    public async Task<DocumentDownload> DownloadAsync(int id, int user, bool isLecturer, CancellationToken ct)
    {
        var doc = await Accessible(id, user, isLecturer, ct);
        return new DocumentDownload(FilePath(doc), doc.ContentType, doc.FileName);
    }

}

public record DocumentDownload(string Path, string ContentType, string FileName);
