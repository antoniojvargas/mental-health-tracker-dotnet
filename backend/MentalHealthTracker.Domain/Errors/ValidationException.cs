namespace MentalHealthTracker.Domain.Errors;

public class ValidationException : AppException
{
    public const int DefaultStatusCode = 400;

    public ValidationException(string message, IReadOnlyDictionary<string, string>? details = null)
        : base(ErrorCodes.ValidationError, DefaultStatusCode, message, details)
    {
    }
}