using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AIVES.Api.Features.Common;
using AIVES.Api.Features.Exams;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using AIVES.Api.Features.Workflow;

namespace AIVES.Api.Hubs;

/// <summary>
/// SignalR hub for real-time exam session communication.
/// Students and lecturers join exam-specific groups.
/// </summary>
[Authorize(Roles = "Student,Lecturer")]
public class ExamHub(ExamQueryService exams, IServiceProvider services) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Groups.AddToGroupAsync(Context.ConnectionId, ScheduleNotifications.UserGroup(user));
        await base.OnConnectedAsync();
    }
    // SignalR duplex streaming: React uploads PCM frames; only backend connects to Google.
    public async IAsyncEnumerable<LiveAttemptEvent> Transcribe(int examId, string turnId, ChannelReader<string> audio,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Context.User!.IsInRole("Student")) throw new HubException("Chỉ sinh viên có thể thu âm cho phiên thi.");
        var live = services.GetRequiredService<LiveAttemptService>();
        var student = int.Parse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.ConnectionAborted);
        await using var reader = live.Stream(examId, student, turnId, audio, cancelled.Token).GetAsyncEnumerator(cancelled.Token);
        while (true)
        {
            bool next;
            try { next = await reader.MoveNextAsync(); }
            catch (WorkflowException ex)
            {
                await live.LogError(examId, student, ex.Message);
                throw new HubException(ex.Message);
            }
            catch (OperationCanceledException)
            {
                if (Context.ConnectionAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested) yield break;
                throw new HubException("LiveDeadline: microphone đã dừng theo giới hạn thời gian phía server; transcript đã xác nhận được giữ.");
            }
            catch (Exception)
            {
                await live.LogError(examId, student, "GeminiLiveUnavailable: lỗi kết nối; hãy thu tiếp hoặc nhập tay.");
                throw new HubException("GeminiLiveUnavailable: lỗi kết nối; transcript đã xác nhận vẫn được giữ.");
            }
            if (!next) break;
            yield return reader.Current;
        }
    }

    public async Task JoinExam(int examId)
    {
        var user = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var caller = new Caller(user, Context.User!.FindFirstValue(ClaimTypes.Role) ?? "");
        var allowed = await exams.CanJoinAsync(examId, caller, Context.ConnectionAborted);
        if (!allowed) throw new HubException("Không có quyền truy cập kỳ thi.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"student-{user}-exam-{examId}");
        await Clients.Caller.SendAsync("ServerTime", DateTimeOffset.UtcNow);
    }

    public async Task LeaveExam(int examId)
    {
        var user = Context.User!.FindFirstValue(ClaimTypes.NameIdentifier);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"student-{user}-exam-{examId}");
    }

    public async Task SendMessage(int examId, string message)
    {
        await Task.CompletedTask;
        throw new HubException("Client không được phát event phiên thi.");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
