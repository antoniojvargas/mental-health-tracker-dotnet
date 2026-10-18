using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealthTracker.Api.Modules.Realtime;

[RequireAuth]
public sealed class LogsHub(SignalRConnectionTracker connectionTracker) : Hub
{
    public static string GroupNameFor(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        // RequireAuth rechaza antes toda conexión sin sesión válida; sin claim sería un fallo del servidor.
        if (!Context.User.TryGetUserId(out var userId))
        {
            throw new InvalidOperationException("The authenticated connection has no valid user id claim.");
        }

        connectionTracker.Track(Context);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(userId));

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connectionTracker.Untrack(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}