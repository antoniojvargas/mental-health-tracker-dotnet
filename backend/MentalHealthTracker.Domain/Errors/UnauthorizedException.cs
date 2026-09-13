namespace MentalHealthTracker.Domain.Errors;

public class UnauthorizedException : AppException
{
    public const int DefaultStatusCode = 401;

    public const string ErrorCode = "UNAUTHORIZED";

    public UnauthorizedException(string message)
        : base(ErrorCode, DefaultStatusCode, message)
    {
    }
}