using System.Text.Json;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Domain.Errors;
using Microsoft.AspNetCore.Http;
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
    public async Task AppException_IsLoggedAsInformation()
    {
        var logger = new CapturingLogger();

        await HandleAsync(new NotFoundException("nope"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.IsType<NotFoundException>(entry.Exception);
    }

    [Fact]
    public async Task GenericException_IsLoggedAsError()
    {
        var logger = new CapturingLogger();

        await HandleAsync(new InvalidOperationException("boom"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    private sealed record Entry(LogLevel Level, Exception? Exception);

    private sealed class CapturingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new Entry(logLevel, exception));
    }
}
