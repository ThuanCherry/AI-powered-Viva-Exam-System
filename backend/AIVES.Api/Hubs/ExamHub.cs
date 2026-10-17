using Microsoft.AspNetCore.SignalR;

namespace AIVES.Api.Hubs;

/// <summary>
/// SignalR hub for real-time exam session communication.
/// Students and lecturers join exam-specific groups.
/// </summary>
public class ExamHub : Hub
{
    public async Task JoinExam(int examId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"exam-{examId}");
        await Clients.Group($"exam-{examId}").SendAsync("UserJoined", Context.ConnectionId);
    }

    public async Task LeaveExam(int examId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"exam-{examId}");
    }

    public async Task SendMessage(int examId, string message)
    {
        await Clients.Group($"exam-{examId}").SendAsync("ReceiveMessage", Context.ConnectionId, message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
