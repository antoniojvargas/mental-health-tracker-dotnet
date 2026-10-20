using System.Text.Json;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MentalHealthTracker.UnitTests;

// Pruebas de caracterización de GlobalExceptionHandler sobre un DefaultHttpContext con el
// cuerpo en memoria: lo que se congela es el sobre {"error":{code,message,details}} que ve
// el cliente, que es el contrato de errores de toda la API.
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(bool Handled, int Status, string ContentType, string Body)> HandleAsync(
        Exception exception,
        ILogger<GlobalExceptionHandler>? logger = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(logger ?? NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (handled, context.Response.StatusCode, context.Response.ContentType ?? string.Empty, body);
    }

    [Fact]
    public async Task AppException_ProducesItsStatusCodeMessageAndDetails()
    {
        var exception = new AppException(
            "CUSTOM_CODE",
            StatusCodes.Status418ImATeapot,
            "Custom message",
            new Dictionary<string, string> { ["Field"] = "Field is wrong", ["Other"] = "Other is wrong" });

        var (handled, status, contentType, body) = await HandleAsync(exception);

        Assert.True(handled);
        Assert.Equal(418, status);
        Assert.StartsWith("application/json", contentType);
        Assert.Equal(
            """{"error":{"code":"CUSTOM_CODE","message":"Custom message","details":[{"field":"Field","message":"Field is wrong"},{"field":"Other","message":"Other is wrong"}]}}""",
            body);
    }

    [Fact]
    public async Task AppException_WithoutDetails_ProducesAnEmptyDetailsArray()
    {
        var (_, status, _, body) = await HandleAsync(new AppException("X", 409, "Conflict"));

        Assert.Equal(409, status);
        Assert.Equal("""{"error":{"code":"X","message":"Conflict","details":[]}}""", body);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), 404, "NOT_FOUND")]
    [InlineData(typeof(UnauthorizedException), 401, "UNAUTHORIZED")]
    public async Task DomainExceptions_ProduceTheirOwnStatusAndCode(Type type, int expectedStatus, string expectedCode)
    {
        var exception = (Exception)Activator.CreateInstance(type, "Some message")!;

        var (_, status, _, body) = await HandleAsync(exception);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(
            $$$"""{"error":{"code":"{{{expectedCode}}}","message":"Some message","details":[]}}""",
            body);
    }

    [Fact]
    public async Task ValidationException_ProducesBadRequestWithItsDetails()
    {
        var exception = new MentalHealthTracker.Domain.Errors.ValidationException(
            "Validation failed",
            new Dictionary<string, string> { ["MoodRating"] = "Must be between 1 and 5" });

        var (_, status, _, body) = await HandleAsync(exception);

        Assert.Equal(400, status);
        Assert.Equal(
            """{"error":{"code":"VALIDATION_ERROR","message":"Validation failed","details":[{"field":"MoodRating","message":"Must be between 1 and 5"}]}}""",
            body);
    }

    [Fact]
    public async Task GenericException_Produces500WithTheExactBody()
    {
        var (handled, status, contentType, body) = await HandleAsync(new InvalidOperationException("secret internal detail"));

        Assert.True(handled);
        Assert.Equal(500, status);
        Assert.StartsWith("application/json", contentType);
        Assert.Equal(
            """{"error":{"code":"INTERNAL_ERROR","message":"An unexpected error occurred.","details":[]}}""",
            body);
    }

    [Fact]
    public async Task GenericException_NeverLeaksItsMessageOrTypeToTheClient()
    {
        var (_, _, _, body) = await HandleAsync(new InvalidOperationException("password=hunter2"));

        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("InvalidOperation", body);
    }

    [Fact]
    public async Task GenericException_BodyIsAnEnvelopeWithASingleErrorProperty()
    {
        var (_, _, _, body) = await HandleAsync(new InvalidOperationException("boom"));

        using var document = JsonDocument.Parse(body);
        Assert.Equal(["error"], document.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task AppException_IsLoggedAsInformationWithoutAStackTrace()
    {
        var logger = new CapturingLogger();

        await HandleAsync(new NotFoundException("nope"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task AppException_LogsItsCodeStatusAndMessageAsStructuredProperties()
    {
        var logger = new CapturingLogger();

        await HandleAsync(new NotFoundException("Route /api/x does not exist"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("NOT_FOUND", entry.Properties["ErrorCode"]);
        Assert.Equal(404, entry.Properties["StatusCode"]);
        Assert.Equal("Route /api/x does not exist", entry.Properties["ErrorMessage"]);
        Assert.Equal("Request rejected with NOT_FOUND (404): Route /api/x does not exist", entry.Message);
    }

    [Fact]
    public async Task TheEmittedLogsCarryTheirStableEventIds()
    {
        var appLogger = new CapturingLogger();
        await HandleAsync(new NotFoundException("nope"), appLogger);
        var unexpectedLogger = new CapturingLogger();
        await HandleAsync(new InvalidOperationException("boom"), unexpectedLogger);

        var rejected = Assert.Single(appLogger.Entries).EventId;
        var unhandled = Assert.Single(unexpectedLogger.Entries).EventId;
        Assert.Equal((1101, "RequestRejected"), (rejected.Id, rejected.Name));
        Assert.Equal((1102, "UnhandledException"), (unhandled.Id, unhandled.Name));
    }

    [Fact]
    public async Task GenericException_IsLoggedAsErrorWithItsStackTrace()
    {
        var logger = new CapturingLogger();

        await HandleAsync(new InvalidOperationException("boom"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task OperationCanceled_WhenTheRequestWasAborted_IsLoggedAsDebugAndWritesNoBody()
    {
        var logger = new CapturingLogger();
        using var aborted = new CancellationTokenSource();
        var context = NewContext(aborted.Token);
        await aborted.CancelAsync();
        var handler = new GlobalExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        Assert.True(handled);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Null(context.Response.ContentType);
        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task TaskCanceled_WhenTheRequestWasAborted_IsTreatedTheSameWay()
    {
        var logger = new CapturingLogger();
        using var aborted = new CancellationTokenSource();
        var context = NewContext(aborted.Token);
        await aborted.CancelAsync();

        await new GlobalExceptionHandler(logger).TryHandleAsync(context, new TaskCanceledException(), CancellationToken.None);

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task OperationCanceled_WhenTheRequestWasNotAborted_IsAnUnexpectedError()
    {
        var logger = new CapturingLogger();
        var context = NewContext(CancellationToken.None);

        var handled = await new GlobalExceptionHandler(logger).TryHandleAsync(
            context, new OperationCanceledException("internal timeout"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Contains("INTERNAL_ERROR", await new StreamReader(context.Response.Body).ReadToEndAsync());
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task OperationCanceled_WhenTheResponseAlreadyStarted_DoesNotTouchTheStatus()
    {
        var logger = new CapturingLogger();
        using var aborted = new CancellationTokenSource();
        var context = NewContext(aborted.Token);
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        await aborted.CancelAsync();

        var handled = await new GlobalExceptionHandler(logger).TryHandleAsync(
            context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    private static DefaultHttpContext NewContext(CancellationToken requestAborted)
    {
        var context = new DefaultHttpContext { RequestAborted = requestAborted };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception, Dictionary<string, object?> Properties, EventId EventId);

    private sealed class CapturingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                : [];
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception, properties, eventId));
        }
    }
}
