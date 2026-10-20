using MentalHealthTracker.Api.Core.Errors;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MentalHealthTracker.Api.Modules.DailyLog;

[ApiController]
[Route("api/logs")]
[RequireAuth]
public sealed class DailyLogController(IDailyLogService dailyLogService) : ControllerBase
{
    internal const string WriteRateLimitPolicy = "write";

    // RequireAuth ya rechazó con 401 toda petición sin sesión válida, así que el claim siempre
    // está; si no lo estuviera sería un fallo del servidor (500), no del cliente.
    private Guid UserId => User.TryGetUserId(out var userId)
        ? userId
        : throw new InvalidOperationException("The authenticated request has no valid user id claim.");

    [HttpPost]
    [EnableRateLimiting(WriteRateLimitPolicy)]
    [ProducesResponseType(typeof(DailyLogResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DailyLogResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors]
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
    [ProducesStandardErrors]
    public async Task<IActionResult> List(
        [FromQuery] ListDailyLogsQuery query,
        CancellationToken cancellationToken)
    {
        var response = await dailyLogService.ListAsync(UserId, query, cancellationToken);

        return Ok(response);
    }

    [HttpGet("today")]
    [ProducesResponseType(typeof(DailyLogResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(NotFound = true)]
    public async Task<IActionResult> GetToday(
        [FromQuery] TodayQuery query,
        CancellationToken cancellationToken)
    {
        var response = await dailyLogService.GetTodayAsync(UserId, query.Date, cancellationToken)
            ?? throw new NotFoundException("No log for that day");

        return Ok(response);
    }
}