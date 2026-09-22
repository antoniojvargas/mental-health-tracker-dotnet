using System.Security.Claims;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealthTracker.Api.Modules.Realtime;

[RequireAuth]
public sealed class LogsHub : Hub
{
    public static string GroupNameFor(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(userId));

        await base.OnConnectedAsync();
    }
}