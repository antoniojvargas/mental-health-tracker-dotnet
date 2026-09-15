using System.Security.Claims;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealthTracker.Api.Modules.DailyLog;

[ApiController]
[Route("api/logs")]
[RequireAuth]
public sealed class DailyLogController(IDailyLogService dailyLogService) : ControllerBase
{
    private Guid UserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Upsert(
        CreateDailyLogRequest request,
        CancellationToken cancellationToken)
    {
        var (response, created) = await dailyLogService.UpsertAsync(UserId, request, cancellationToken);

        return created
            ? StatusCode(StatusCodes.Status201Created, response)
            : Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ListDailyLogsQuery query,
        CancellationToken cancellationToken)
    {
        var response = await dailyLogService.ListAsync(UserId, query, cancellationToken);

        return Ok(response);
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday(CancellationToken cancellationToken)
    {
        var response = await dailyLogService.GetTodayAsync(UserId, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}