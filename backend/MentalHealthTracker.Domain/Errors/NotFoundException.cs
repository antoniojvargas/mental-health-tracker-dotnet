namespace MentalHealthTracker.Domain.Errors;

public class NotFoundException : AppException
{
    public const int DefaultStatusCode = 404;

    public const string ErrorCode = "NOT_FOUND";

    public NotFoundException(string message)
        : base(ErrorCode, DefaultStatusCode, message)
    {
    }
}