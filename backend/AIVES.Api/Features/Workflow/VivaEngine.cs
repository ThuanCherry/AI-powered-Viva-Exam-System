using AIVES.Api.Data;
using AIVES.Api.Integrations;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Workflow;

public class VivaEngine(AivesDbContext db, WorkflowService flow, IVivaLlm llm, IVivaSpeech speech, ProviderStatus providers, IConfiguration? configuration = null)
{
    public async Task<ExamCandidate> Candidate(int examId, int student, CancellationToken ct)
    {
        var c = await db.ExamCandidates.Include(c => c.ExamSession).ThenInclude(e => e!.VivaSettings)
            .FirstOrDefaultAsync(c => c.ExamId == examId && c.StudentId == student, ct);
        Require(c != null, "Bạn không có trong danh sách thi.", 403);
        return c!;
    }
    public DateTime Deadline(ExamCandidate c) => new[] { c.ExamSession!.EndTime,
        c.ScheduledTime!.Value.AddMinutes(c.ExamSession.TimePerStudent), c.StartedAt!.Value.AddMinutes(c.ExamSession.TimePerStudent) }.Min();

    private async Task<List<ExamQuestion>> Questions(ExamCandidate c, CancellationToken ct) => await db.ExamQuestions
        .Include(q => q.Question).Include(q => q.Answer).ThenInclude(a => a!.FollowUps)
        .Where(q => q.CandidateId == c.CandidateId && q.ParentExamQuestionId == null).OrderBy(q => q.OrderNo).ToListAsync(ct);

    private static (ExamQuestion? Main, FollowUpQuestion? Follow) Current(List<ExamQuestion> qs)
    {
        foreach (var q in qs)
        {
            var f = q.Answer?.FollowUps.OrderBy(f => f.OrderNo).FirstOrDefault(f => f.AnsweredAt == null);
            if (f != null) return (q, f);
            if (!q.IsCompleted) return (q, null);
        }
        return (null, null);
    }
    public static string Turn(ExamQuestion q, FollowUpQuestion? f) => f == null ? $"q-{q.ExamQuestionId}" : $"f-{f.FollowUpId}";

    public async Task Advance(ExamCandidate c, CancellationToken ct)
    {
        if (c.Status != "InProgress") return;
        var qs = await Questions(c, ct);
        var (main, follow) = Current(qs);
        var now = flow.Now;
        if (now >= Deadline(c))
        {
            if (main != null) CloseTurn(main, follow, now, true);
            await Complete(c, ct); return;
        }
        if (main != null)
        {
            var asked = follow?.AskedAt ?? main.AskedAt;
            if (asked.HasValue && now >= asked.Value.AddSeconds(c.ExamSession!.VivaSettings?.SecondsPerQuestion ?? 120))
            {
                CloseTurn(main, follow, now, true);
                flow.Log(c.ExamId, c.CandidateId, "TurnTimeout", Turn(main, follow));
                await db.SaveChangesAsync(ct);
                (main, follow) = Current(qs);
            }
        }
        if (main == null) { await Complete(c, ct); return; }
        if (follow == null && main.AskedAt == null) main.AskedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private void CloseTurn(ExamQuestion main, FollowUpQuestion? follow, DateTime now, bool timeout)
    {
        if (follow != null) { follow.AnsweredAt = now; return; }
        main.IsCompleted = true;
        main.Answer ??= new Answer { CandidateId = main.CandidateId, ExamQuestionId = main.ExamQuestionId, StartedAt = main.AskedAt, CreatedAt = now };
        main.Answer.FinishedAt = now;
        main.Answer.DurationSeconds = (int)Math.Max(0, (now - (main.AskedAt ?? now)).TotalSeconds);
    }

    private async Task Complete(ExamCandidate c, CancellationToken ct)
    {
        c.Status = "Completed"; c.FinishedAt = flow.Now;
        flow.Log(c.ExamId, c.CandidateId, "AttemptCompleted");
        await db.SaveChangesAsync(ct);
        if (!await db.ExamCandidates.AnyAsync(x => x.ExamId == c.ExamId && (x.Status == "Waiting" || x.Status == "InProgress"), ct))
        { c.ExamSession!.Status = "Completed"; await db.SaveChangesAsync(ct); }
    }

    public async Task<object> State(ExamCandidate c, CancellationToken ct)
    {
        var qs = await Questions(c, ct);
        var (main, follow) = c.Status == "InProgress" ? Current(qs) : (null, null);
        var now = flow.Now;
        var deadline = c.StartedAt != null ? Deadline(c) : (DateTime?)null;
        var turnEnd = main == null ? (DateTime?)null : new[] { (follow?.AskedAt ?? main.AskedAt ?? now)
            .AddSeconds(c.ExamSession!.VivaSettings?.SecondsPerQuestion ?? 120), deadline!.Value }.Min();
        var warning = await db.ExamLogs.Where(l => l.CandidateId == c.CandidateId && l.Action == "ProviderError")
            .OrderByDescending(l => l.LogId).Select(l => l.Description).FirstOrDefaultAsync(ct);
        return new { c.ExamId, c.CandidateId, c.Status, serverTime = WorkflowService.Iso(now),
            deadline = deadline.HasValue ? WorkflowService.Iso(deadline.Value) : null,
            scheduledTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value) : null,
            turnDeadline = turnEnd.HasValue ? WorkflowService.Iso(turnEnd.Value) : null,
            mainQuestionCount = qs.Count, completedMain = qs.Count(q => q.IsCompleted),
            followUpsUsed = qs.Sum(q => q.Answer?.FollowUps.Count ?? 0), maxFollowUpCount = c.ExamSession!.MaxFollowUpCount,
            turn = main == null ? null : new { turnId = Turn(main, follow), text = follow?.QuestionText ?? main.Question!.QuestionText,
                mainOrder = main.OrderNo, isFollowUp = follow != null, transcript = follow?.AnswerTranscript ?? (follow == null ? main.Answer?.Transcript : null) ?? "" },
            history = qs.Where(q => q.AskedAt != null).Select(q => new { question = q.Question!.QuestionText, transcript = q.Answer?.Transcript,
                completed = q.IsCompleted, askedAt = WorkflowService.Iso(q.AskedAt!.Value),
                followUps = q.Answer?.FollowUps.OrderBy(f => f.OrderNo).Select(f => new { question = f.QuestionText, transcript = f.AnswerTranscript, answered = f.AnsweredAt != null }) }),
            providers = providers.View, providerWarning = warning };
    }

    public async Task<object> Start(int exam, int student, CancellationToken ct)
    {
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct);
        if (c.Status == "Waiting")
        {
            Require(c.ExamSession!.Status is "Scheduled" or "InProgress", "Kỳ thi chưa được công bố.", 409);
            var now = flow.Now;
            Require(c.ScheduledTime.HasValue && now >= c.ScheduledTime && now >= c.ExamSession.StartTime
                && now < c.ScheduledTime.Value.AddMinutes(c.ExamSession.TimePerStudent) && now < c.ExamSession.EndTime, "Chưa tới slot hoặc slot đã hết giờ.", 409);
            Require(await db.ExamQuestions.CountAsync(q => q.CandidateId == c.CandidateId && q.ParentExamQuestionId == null, ct) == c.ExamSession.MainQuestionCount, "Chưa phân đủ câu chính; liên hệ giảng viên.", 409);
            c.Status = "InProgress"; c.StartedAt = now; c.ExamSession.Status = "InProgress";
            flow.Log(exam, c.CandidateId, "AttemptStarted"); await db.SaveChangesAsync(ct);
        }
        Require(c.Status is "InProgress" or "Completed", "Phiên thi không khả dụng.", 409);
        await Advance(c, ct); var state = await State(c, ct); await tx.CommitAsync(ct); return state;
    }

    public async Task<object> Resume(int exam, int student, CancellationToken ct)
    {
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct); await Advance(c, ct);
        var state = await State(c, ct); await tx.CommitAsync(ct); return state;
    }

    public async Task<object> Submit(int exam, int student, AnswerInput input, CancellationToken ct)
    {
        Require(input.Transcript != null && input.Transcript.Length <= 12000, "Transcript tối đa 12000 ký tự.");
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct); await Advance(c, ct);
        var qs = await Questions(c, ct); var (main, follow) = Current(qs);
        if (c.Status == "Completed" || main == null || Turn(main, follow) != input.TurnId)
        {
            var already = qs.Any(q => Turn(q, null) == input.TurnId && q.IsCompleted
                || (q.Answer?.FollowUps.Any(f => Turn(q, f) == input.TurnId && f.AnsweredAt != null) ?? false));
            Require(already, "Lượt trả lời đã thay đổi; tải lại trạng thái.", 409);
            var old = await State(c, ct); await tx.CommitAsync(ct); return old;
        }
        Require(c.Status == "InProgress", "Phiên thi chưa bắt đầu.", 409);
        var current = follow?.QuestionText ?? main!.Question!.QuestionText;
        if (follow != null) follow.AnswerTranscript = input.Transcript.Trim();
        else
        {
            main!.Answer ??= new Answer { CandidateId = c.CandidateId, ExamQuestionId = main.ExamQuestionId, CreatedAt = flow.Now, StartedAt = main.AskedAt };
            main.Answer.Transcript = input.Transcript.Trim();
        }
        CloseTurn(main, follow, flow.Now, false);
        await db.SaveChangesAsync(ct);
        var perMain = main.Answer!.FollowUps.Count;
        // Disabled by default: submitting only persists the answer and advances the exam.
        // Keep the existing follow-up implementation for an explicit future opt-in.
        if (configuration?.GetValue<bool>("AI:EnableFollowUpOnSubmit") == true
            && !string.IsNullOrWhiteSpace(input.Transcript) && CanFollowUp(qs.Sum(q => q.Answer?.FollowUps.Count ?? 0), perMain,
            c.ExamSession!.MaxFollowUpCount, c.ExamSession.VivaSettings?.MaxFollowUpsPerQuestion ?? 1, flow.Now, Deadline(c)))
        {
            try
            {
                var history = string.Join("\n", main.Answer.FollowUps.Select(f => $"Q:{f.QuestionText}\nA:{f.AnswerTranscript}"));
                var rubric = await db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.QuestionId == main.QuestionId, ct);
                var expected = (main.Question!.ExpectedAnswer ?? "") + (rubric == null ? "" : "\nRubric: " + string.Join("; ", rubric.Criteria.OrderBy(r => r.DisplayOrder).Select(r => $"{r.CriteriaName}: {r.Description}")));
                var decision = await llm.Decide(main.Question.QuestionText, expected, current, input.Transcript, history, ct);
                if (decision.NeedsFollowUp && !string.IsNullOrWhiteSpace(decision.Question) && decision.Question.Length <= 600 && flow.Now < Deadline(c) && !string.Equals(decision.Question.Trim(), main.Question.QuestionText.Trim(), StringComparison.OrdinalIgnoreCase)
                    && !main.Answer.FollowUps.Any(f => string.Equals(f.QuestionText.Trim(), decision.Question?.Trim(), StringComparison.OrdinalIgnoreCase)))
                    main.Answer.FollowUps.Add(new() { QuestionText = decision.Question!.Trim(), OrderNo = perMain + 1, AskedAt = flow.Now,
                        Reason = decision.Reason is { Length: > 1000 } ? decision.Reason[..1000] : decision.Reason });
            }
            catch (WorkflowException ex) { flow.Log(exam, c.CandidateId, "ProviderError", ex.Message); }
        }
        flow.Log(exam, c.CandidateId, "AnswerSubmitted", input.TurnId);
        await db.SaveChangesAsync(ct); await Advance(c, ct);
        var result = await State(c, ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<object> Transcribe(int exam, int student, string turnId, string chunkId, byte[] wav, CancellationToken ct)
    {
        Require(Guid.TryParse(chunkId, out _), "ChunkId cần UUID.");
        PcmWave.ValidateInput(wav);
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct); await Advance(c, ct);
        var qs = await Questions(c, ct); var (main, follow) = Current(qs);
        Require(c.Status == "InProgress" && main != null && Turn(main, follow) == turnId, "Lượt thu âm đã hết hạn.", 409);
        if (!await db.ExamLogs.AnyAsync(l => l.CandidateId == c.CandidateId && l.Action == "AudioChunk" && l.Description == chunkId, ct))
        {
            try
            {
                var transcript = await speech.Transcribe(wav, ct);
                var expires = (follow?.AskedAt ?? main!.AskedAt)!.Value.AddSeconds(c.ExamSession!.VivaSettings?.SecondsPerQuestion ?? 120);
                Require(flow.Now < Deadline(c) && flow.Now < expires, "STT trả về sau deadline; transcript không được nhận.", 409);
                if (follow != null) follow.AnswerTranscript = Append(follow.AnswerTranscript, transcript);
                else
                {
                    main!.Answer ??= new Answer { ExamQuestionId = main.ExamQuestionId, CandidateId = c.CandidateId, CreatedAt = flow.Now, StartedAt = main.AskedAt };
                    main.Answer.Transcript = Append(main.Answer.Transcript, transcript);
                }
                flow.Log(exam, c.CandidateId, "AudioChunk", chunkId); await db.SaveChangesAsync(ct);
            }
            catch (WorkflowException ex)
            {
                flow.Log(exam, c.CandidateId, "ProviderError", ex.Message); await db.SaveChangesAsync(CancellationToken.None);
                await tx.CommitAsync(CancellationToken.None); throw;
            }
        }
        var state = await State(c, ct); await tx.CommitAsync(ct); return state;
    }

    private static string Append(string? existing, string text) => ((existing ?? "") + " " + text).Trim()[..Math.Min(12000, ((existing ?? "") + " " + text).Trim().Length)];

    public async Task<LiveTurnGrant> AudioGrant(int exam, int student, string turnId, CancellationToken ct)
    {
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct); await Advance(c, ct);
        var (main, follow) = Current(await Questions(c, ct));
        Require(c.Status == "InProgress" && main != null && Turn(main, follow) == turnId, "Lượt microphone không còn mở hoặc đã hết giờ.", 409);
        var expires = new[] { Deadline(c), (follow?.AskedAt ?? main.AskedAt)!.Value.AddSeconds(c.ExamSession!.VivaSettings?.SecondsPerQuestion ?? 120) }.Min();
        var transcript = follow?.AnswerTranscript ?? (follow == null ? main.Answer?.Transcript : null) ?? "";
        await tx.CommitAsync(ct); return new(expires, transcript);
    }

    public async Task<string> PersistLiveTranscript(int exam, int student, string turnId, string segmentId, string text, CancellationToken ct)
    {
        Require(text.Length <= 12000 && segmentId.Length <= 80, "Transcript/segment quá dài.");
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct); await Advance(c, ct);
        var (main, follow) = Current(await Questions(c, ct));
        Require(c.Status == "InProgress" && main != null && Turn(main, follow) == turnId, "Transcript đến sau khi lượt đã đóng; không nhận.", 409);
        if (!await db.ExamLogs.AnyAsync(l => l.CandidateId == c.CandidateId && l.Action == "LiveSegment" && l.Description == segmentId, ct))
        {
            if (follow != null) follow.AnswerTranscript = Append(follow.AnswerTranscript, text);
            else
            {
                main.Answer ??= new Answer { ExamQuestionId = main.ExamQuestionId, CandidateId = c.CandidateId, CreatedAt = flow.Now, StartedAt = main.AskedAt };
                main.Answer.Transcript = Append(main.Answer.Transcript, text);
            }
            flow.Log(exam, c.CandidateId, "LiveSegment", segmentId); await db.SaveChangesAsync(ct);
        }
        var transcript = follow?.AnswerTranscript ?? (follow == null ? main.Answer?.Transcript : null) ?? "";
        await tx.CommitAsync(ct); return transcript;
    }

    public async Task LogSpeechError(int exam, int student, string message, CancellationToken ct)
    {
        var c = await Candidate(exam, student, ct);
        flow.Log(exam, c.CandidateId, "ProviderError", message.Length > 1900 ? message[..1900] : message);
        await db.SaveChangesAsync(ct);
    }

    public async Task<byte[]> Speak(int exam, int student, string turnId, CancellationToken ct)
    {
        await using var tx = await flow.LockExam(exam, ct);
        var c = await Candidate(exam, student, ct); await Advance(c, ct);
        var (main, follow) = Current(await Questions(c, ct));
        Require(c.Status == "InProgress" && main != null && Turn(main, follow) == turnId, "Không có lượt TTS hợp lệ.", 409);
        try
        {
            var audio = await speech.Speak(follow?.QuestionText ?? main!.Question!.QuestionText, ct);
            await tx.CommitAsync(ct); return audio;
        }
        catch (WorkflowException ex)
        {
            flow.Log(exam, c.CandidateId, "ProviderError", ex.Message); await db.SaveChangesAsync(CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None); throw;
        }
    }

    public async Task Sweep(int examId, CancellationToken ct)
    {
        await using var tx = await flow.LockExam(examId, ct);
        var exam = await db.ExamSessions.Include(e => e.VivaSettings).Include(e => e.Candidates).FirstOrDefaultAsync(e => e.ExamId == examId, ct);
        if (exam == null || exam.Status is not ("Scheduled" or "InProgress")) { await tx.CommitAsync(ct); return; }
        foreach (var c in exam.Candidates)
        {
            if (c.Status == "Waiting" && (flow.Now >= exam.EndTime || c.ScheduledTime.HasValue && flow.Now >= c.ScheduledTime.Value.AddMinutes(exam.TimePerStudent)))
            { c.Status = "Absent"; c.FinishedAt = flow.Now; flow.Log(examId, c.CandidateId, "SlotExpired"); }
            else if (c.Status == "InProgress") await Advance(c, ct);
        }
        if (exam.Candidates.All(c => c.Status is not ("Waiting" or "InProgress"))) exam.Status = "Completed";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

public class DeadlineWorker(IServiceScopeFactory scopes, ILogger<DeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
                var exams = await db.ExamSessions.AsNoTracking().Where(e => e.Status == "Scheduled" || e.Status == "InProgress")
                    .Select(e => e.ExamId).ToListAsync(stoppingToken);
                foreach (var id in exams)
                {
                    using var attempt = scopes.CreateScope();
                    await attempt.ServiceProvider.GetRequiredService<VivaEngine>().Sweep(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Deadline sweep unavailable ({Type}). Check database configuration.", ex.GetType().Name); }
        }
    }
}
