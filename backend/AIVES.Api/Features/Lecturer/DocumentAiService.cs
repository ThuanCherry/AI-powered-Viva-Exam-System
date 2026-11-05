using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace AIVES.Api.Features.Lecturer;

public record GenerateQuestionsRequest(int Count = 5);

public record GeneratedQuestionItem(
    string QuestionText,
    string ExpectedAnswer,
    string Difficulty,
    string BloomLevel,
    string? Topic,
    string? Explanation
);

public class DocumentAiService(
    AivesDbContext db,
    DocumentStore documents,
    LecturerService lecturer,
    IConfiguration config,
    ProviderStatus status,
    ProviderHttp http
)
{
    public async Task<string> ExtractTextAsync(Document doc, CancellationToken ct)
    {
        var filePath = documents.FilePath(doc);
        WorkflowRules.Require(File.Exists(filePath), "Không tìm thấy file tài liệu trên máy chủ.", 404);

        var ext = Path.GetExtension(doc.FileName).ToLowerInvariant();
        if (ext == ".pdf")
        {
            return await Task.Run(() =>
            {
                using var pdf = PdfDocument.Open(filePath);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    var text = page.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
                return sb.ToString().Trim();
            }, ct);
        }
        else if (ext == ".docx")
        {
            return await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(filePath);
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null) return string.Empty;
                using var stream = entry.Open();
                var xdoc = XDocument.Load(stream);
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var paragraphs = xdoc.Descendants(w + "p")
                    .Select(p => string.Join("", p.Descendants(w + "t").Select(t => t.Value)).Trim())
                    .Where(s => !string.IsNullOrEmpty(s));
                return string.Join("\n", paragraphs);
            }, ct);
        }

        throw new WorkflowException(415, "Định dạng file không hỗ trợ trích xuất văn bản.");
    }

    public async Task<List<DocumentChunk>> ChunkAndStoreAsync(Document doc, string fullText, CancellationToken ct)
    {
        // 1. Remove prior chunks if re-processing
        var oldChunks = await db.DocumentChunks.Where(c => c.DocumentId == doc.DocumentId).ToListAsync(ct);
        if (oldChunks.Count > 0)
        {
            db.DocumentChunks.RemoveRange(oldChunks);
        }

        // 2. Split fullText into chunks ~1500 chars
        var chunks = new List<DocumentChunk>();
        var paragraphs = fullText.Split(["\r\n\r\n", "\n\n", "\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var p in paragraphs)
        {
            var trimmed = p.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (currentChunk.Length + trimmed.Length > 1800 && currentChunk.Length > 0)
            {
                var content = currentChunk.ToString().Trim();
                chunks.Add(new DocumentChunk
                {
                    DocumentId = doc.DocumentId,
                    ChunkIndex = chunkIndex++,
                    Content = content,
                    TokenCount = Math.Max(1, content.Length / 4),
                    EmbeddingJson = "[]"
                });
                currentChunk.Clear();
            }

            if (currentChunk.Length > 0) currentChunk.Append("\n");
            currentChunk.Append(trimmed);
        }

        if (currentChunk.Length > 0)
        {
            var content = currentChunk.ToString().Trim();
            chunks.Add(new DocumentChunk
            {
                DocumentId = doc.DocumentId,
                ChunkIndex = chunkIndex++,
                Content = content,
                TokenCount = Math.Max(1, content.Length / 4),
                EmbeddingJson = "[]"
            });
        }

        if (chunks.Count > 0)
        {
            db.DocumentChunks.AddRange(chunks);
        }

        doc.Status = "Processed";
        doc.ProcessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return chunks;
    }

    public async Task<object> GenerateQuestionsFromDocumentAsync(int documentId, int userId, int requestedCount, CancellationToken ct)
    {
        var doc = await db.Documents.Include(d => d.Course).FirstOrDefaultAsync(d => d.DocumentId == documentId && d.Status != "Archived", ct);
        WorkflowRules.Require(doc != null, "Tài liệu không tồn tại hoặc đã bị gỡ.", 404);
        await lecturer.Course(doc!.CourseId, userId, ct);

        var count = Math.Clamp(requestedCount, 1, 15);

        // 1. Ensure text extracted and chunks stored
        var existingChunks = await db.DocumentChunks.Where(c => c.DocumentId == documentId).OrderBy(c => c.ChunkIndex).ToListAsync(ct);
        string textForAi;

        if (existingChunks.Count == 0)
        {
            var extracted = await ExtractTextAsync(doc, ct);
            WorkflowRules.Require(!string.IsNullOrWhiteSpace(extracted) && extracted.Length >= 20,
                "Không thể trích xuất văn bản từ tài liệu (tài liệu rỗng hoặc dạng scan ảnh không có chữ).", 422);

            existingChunks = await ChunkAndStoreAsync(doc, extracted, ct);
            textForAi = extracted;
        }
        else
        {
            textForAi = string.Join("\n\n", existingChunks.Select(c => c.Content));
        }

        // Limit excerpt to ~25,000 characters to stay well within token limits and guarantee fast latency
        if (textForAi.Length > 25000)
        {
            textForAi = textForAi[..25000];
        }

        // 2. Call AI to generate viva questions
        List<GeneratedQuestionItem> items;
        if (status.Llm)
        {
            items = await CallGeminiGenerateQuestions(textForAi, count, ct);
        }
        else
        {
            // Deterministic fallback for offline / test environments
            items = GenerateFallbackQuestions(existingChunks, count);
        }

        WorkflowRules.Require(items.Count > 0, "AI không sinh được câu hỏi nào từ nội dung tài liệu.", 502);

        // 3. Persist into generated_questions AND questions (Question Bank)
        var now = DateTime.UtcNow;
        var createdQuestions = new List<Question>();

        foreach (var item in items)
        {
            var cleanDifficulty = item.Difficulty switch
            {
                "Hard" => "Hard",
                "Medium" => "Medium",
                _ => "Easy"
            };

            var cleanBloom = item.BloomLevel switch
            {
                "Analyze" => "Analyze",
                "Apply" => "Apply",
                "Understand" => "Understand",
                _ => "Remember"
            };

            var bloomInt = cleanBloom switch
            {
                "Analyze" => 4,
                "Apply" => 3,
                "Understand" => 2,
                _ => 1
            };

            var qText = item.QuestionText.Trim();
            var expAns = string.IsNullOrWhiteSpace(item.ExpectedAnswer) ? "Cần hiểu đúng khái niệm và trình bày mạch lạc." : item.ExpectedAnswer.Trim();

            // Save record in generated_questions
            var genRecord = new GeneratedQuestion
            {
                DocumentId = doc.DocumentId,
                CourseId = doc.CourseId,
                QuestionText = qText,
                AnswerKey = expAns.Length > 2000 ? expAns[..2000] : expAns,
                Explanation = item.Explanation != null && item.Explanation.Length > 2000 ? item.Explanation[..2000] : item.Explanation,
                BloomLevel = bloomInt,
                Topic = item.Topic != null && item.Topic.Length > 200 ? item.Topic[..200] : item.Topic,
                Status = "Draft",
                ReviewedById = null,
                ReviewedAt = null,
                CreatedAt = now
            };
            db.GeneratedQuestions.Add(genRecord);

            // Save record in Question Bank (questions table) as Draft so lecturer can review & approve
            var bankQuestion = new Question
            {
                CourseId = doc.CourseId,
                CreatedBy = userId,
                QuestionText = qText,
                ExpectedAnswer = expAns,
                Difficulty = cleanDifficulty,
                BloomLevel = cleanBloom,
                QuestionType = "Main",
                Status = "Draft",
                Source = "AI",
                ReviewedBy = null,
                ReviewedAt = null,
                CreatedAt = now
            };
            db.Questions.Add(bankQuestion);
            createdQuestions.Add(bankQuestion);
        }

        await db.SaveChangesAsync(ct);

        return new
        {
            documentId = doc.DocumentId,
            documentName = doc.FileName,
            generatedCount = createdQuestions.Count,
            questions = createdQuestions.Select(q => new
            {
                q.QuestionId,
                q.QuestionText,
                q.ExpectedAnswer,
                q.Difficulty,
                q.BloomLevel,
                q.Status,
                q.Source
            })
        };
    }

    private async Task<List<GeneratedQuestionItem>> CallGeminiGenerateQuestions(string documentText, int count, CancellationToken ct)
    {
        var model = config["AI:Model"] ?? "gemini-3.5-flash";
        WorkflowRules.Require(Regex.IsMatch(model, "^[a-zA-Z0-9.-]+$"), "AI__Model không hợp lệ.", 503);

        var systemPrompt = "Bạn là chuyên gia khảo thí đại học hàng đầu về phỏng vấn vấn đáp (viva exam). Nhiệm vụ của bạn là đọc kỹ tài liệu bài giảng được cung cấp và sinh ra các câu hỏi thi vấn đáp trọng tâm, có tính phân loại cao, kèm đáp án chuẩn chi tiết để giảng viên đánh giá trực tiếp sinh viên.";

        var prompt = "Dưới đây là nội dung trích xuất từ tài liệu bài giảng/giáo trình:\n\"\"\"\n"
            + documentText
            + "\n\"\"\"\n\nHãy sinh đúng " + count + " câu hỏi thi vấn đáp (viva exam) độc lập, bao quát các phần quan trọng của tài liệu trên.\n"
            + "Yêu cầu cụ thể:\n"
            + "1. questionText: Câu hỏi vấn đáp rõ ràng, đánh giá sự hiểu bản chất vấn đề thay vì chỉ học vẹt.\n"
            + "2. expectedAnswer: Đáp án chuẩn hoặc dàn ý cốt lõi sinh viên cần trả lời để đạt điểm tối đa.\n"
            + "3. difficulty: Chọn 1 trong 3 mức: 'Easy', 'Medium', 'Hard'.\n"
            + "4. bloomLevel: Chọn 1 trong 4 mức: 'Remember', 'Understand', 'Apply', 'Analyze'.\n"
            + "5. topic: Tên chủ đề/bài học trích từ tài liệu.\n"
            + "6. explanation: Giải thích ngắn gọn mục đích câu hỏi.\n\n"
            + "Trả về duy nhất định dạng JSON array với các trường trên, không kèm markdown hay văn bản ngoài JSON.";

        var payload = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.3, maxOutputTokens = 4096 }
        });

        var bytes = await http.Send(() =>
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
            r.Headers.Add("x-goog-api-key", config["AI:ApiKey"]);
            r.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            return r;
        }, ct);

        try
        {
            using var jsonDoc = JsonDocument.Parse(bytes);
            var text = jsonDoc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()!;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<GeneratedQuestionItem>>(text, options);
            return list?.Where(q => !string.IsNullOrWhiteSpace(q.QuestionText)).ToList() ?? [];
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new WorkflowException(502, "ProviderInvalidResponse: Gemini không trả đúng định dạng danh sách câu hỏi JSON.");
        }
    }

    private static List<GeneratedQuestionItem> GenerateFallbackQuestions(List<DocumentChunk> chunks, int count)
    {
        var list = new List<GeneratedQuestionItem>();
        for (var i = 0; i < count; i++)
        {
            var chunk = chunks[i % chunks.Count];
            var snippet = chunk.Content.Length > 120 ? chunk.Content[..120] + "..." : chunk.Content;
            list.Add(new GeneratedQuestionItem(
                QuestionText: $"Trình bày và giải thích nội dung cốt lõi của phần kiến thức sau: \"{snippet}\"?",
                ExpectedAnswer: $"Sinh viên cần nêu rõ khái niệm, ý nghĩa và áp dụng thực tiễn được đề cập trong: {snippet}",
                Difficulty: i % 3 == 0 ? "Easy" : i % 3 == 1 ? "Medium" : "Hard",
                BloomLevel: i % 4 == 0 ? "Remember" : i % 4 == 1 ? "Understand" : i % 4 == 2 ? "Apply" : "Analyze",
                Topic: "Kiến thức trọng tâm từ tài liệu",
                Explanation: "Câu hỏi đánh giá mức độ tiếp thu tài liệu học phần."
            ));
        }
        return list;
    }
}
