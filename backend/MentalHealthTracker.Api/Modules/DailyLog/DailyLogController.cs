using System.Security.Claims;
using MentalHealthTracker.Api.Core.Exceptions;
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
    [ProducesResponseType(typeof(DailyLogResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DailyLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(DailyLogListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(
        [FromQuery] ListDailyLogsQuery query,
        CancellationToken cancellationToken)
    {
        var response = await dailyLogService.ListAsync(UserId, query, cancellationToken);

        return Ok(response);
    }

    [HttpGet("today")]
    [ProducesResponseType(typeof(DailyLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetToday(CancellationToken cancellationToken)
    {
        var response = await dailyLogService.GetTodayAsync(UserId, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}