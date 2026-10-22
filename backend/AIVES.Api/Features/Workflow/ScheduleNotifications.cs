using AIVES.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AIVES.Api.Features.Workflow;

public class ScheduleNotifications(IHubContext<ExamHub> hub, ILogger<ScheduleNotifications> logger)
{
    public static string UserGroup(int user) => $"user-{user}";
    public async Task Changed(int examId, int lecturer, IEnumerable<int> students, string reason)
    {
        try
        {
            var groups = students.Append(lecturer).Distinct().Select(UserGroup).ToArray();
            await hub.Clients.Groups(groups).SendAsync("ScheduleChanged", new { examId, reason, serverTime = DateTimeOffset.UtcNow });
        }
        catch (Exception ex) { logger.LogWarning("Schedule notification unavailable ({Type}); committed schedule is retained.", ex.GetType().Name); }
    }
}
