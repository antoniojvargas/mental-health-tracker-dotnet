namespace MentalHealthTracker.Domain.Errors;

public class AppException : Exception
{
    public AppException(
        string code,
        int statusCode,
        string message,
        IReadOnlyDictionary<string, string>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details ?? new Dictionary<string, string>();
    }

    public int StatusCode { get; }

    public string Code { get; }

    public IReadOnlyDictionary<string, string> Details { get; }
}