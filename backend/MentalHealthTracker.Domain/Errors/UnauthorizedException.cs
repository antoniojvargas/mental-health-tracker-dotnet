namespace MentalHealthTracker.Domain.Errors;

public class UnauthorizedException : AppException
{
    public const int DefaultStatusCode = 401;

    public UnauthorizedException(string message)
        : base(ErrorCodes.Unauthorized, DefaultStatusCode, message)
    {
    }
}