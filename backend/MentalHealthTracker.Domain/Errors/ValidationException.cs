namespace MentalHealthTracker.Domain.Errors;

public class ValidationException : AppException
{
    public const int DefaultStatusCode = 400;

    public const string ErrorCode = "VALIDATION_ERROR";

    public ValidationException(string message, IReadOnlyDictionary<string, string>? details = null)
        : base(ErrorCode, DefaultStatusCode, message, details)
    {
    }
}