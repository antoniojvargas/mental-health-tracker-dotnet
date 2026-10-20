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
        // El cliente ya no está (cerró la conexión o abortó la petición): no hay a quién escribirle
        // un cuerpo y no es un error del servidor. Se anota en Debug y se termina.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger);
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodeForAbortedRequest;
            }

            return true;
        }

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
                new ErrorBody(ErrorCodes.InternalError, "An unexpected error occurred.", [])),
        };

        // Un AppException es un resultado esperado de la API (validación, 401, 404...): Information,
        // con el código como propiedad estructurada y sin volcar la traza. Solo lo inesperado
        // lleva la excepción entera (con su stack) en nivel Error.
        if (exception is AppException appError)
        {
            LogHandledAppException(logger, appError.Code, appError.StatusCode, appError.Message);
        }
        else
        {
            LogHandledUnexpectedException(logger, exception);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(body), cancellationToken);
        return true;
    }

    // 499 (el que usa nginx para "el cliente cerró la conexión"): sin él la petición abortada
    // figuraría como un 200 en los registros de acceso y las métricas.
    private const int StatusCodeForAbortedRequest = 499;

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected with {ErrorCode} ({StatusCode}): {ErrorMessage}")]
    private static partial void LogHandledAppException(ILogger logger, string errorCode, int statusCode, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing request")]
    private static partial void LogHandledUnexpectedException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request aborted by the client")]
    private static partial void LogRequestAborted(ILogger logger);
}

public sealed record ErrorResponse(ErrorBody Error);

public sealed record ErrorBody(string Code, string Message, IReadOnlyList<ErrorDetail> Details);

public sealed record ErrorDetail(string Field, string Message);