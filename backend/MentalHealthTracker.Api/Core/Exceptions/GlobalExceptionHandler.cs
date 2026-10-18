using Microsoft.AspNetCore.Diagnostics;
using MentalHealthTracker.Domain.Errors;

namespace MentalHealthTracker.Api.Core.Exceptions;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, body) = exception switch
        {
            AppException appException => (
                appException.StatusCode,
                new ErrorBody(
                    appException.Code,
                    appException.Message,
                    appException.Details
                        .Select(detail => new ErrorDetail(detail.Key, detail.Value))
                        .ToArray())),
            _ => (
                StatusCodes.Status500InternalServerError,
                new ErrorBody("INTERNAL_ERROR", "An unexpected error occurred.", [])),
        };

        if (exception is AppException)
        {
            LogHandledAppException(logger, exception);
        }
        else
        {
            LogHandledUnexpectedException(logger, exception);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(body), cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Exception handled while processing request")]
    private static partial void LogHandledAppException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception handled while processing request")]
    private static partial void LogHandledUnexpectedException(ILogger logger, Exception exception);
}

public sealed record ErrorResponse(ErrorBody Error);

public sealed record ErrorBody(string Code, string Message, IReadOnlyList<ErrorDetail> Details);

public sealed record ErrorDetail(string Field, string Message);