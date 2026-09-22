using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealthTracker.Api.Modules.Realtime;

[RequireAuth]
public sealed class LogsHub : Hub;