namespace MentalHealthTracker.Domain.Errors;

public class NotFoundException : AppException
{
    public const int DefaultStatusCode = 404;

    public NotFoundException(string message)
        : base(ErrorCodes.NotFound, DefaultStatusCode, message)
    {
    }
}