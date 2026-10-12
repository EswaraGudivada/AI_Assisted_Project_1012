namespace UrlShortener.Application;

/// <summary>
/// Custom exception for URL validation errors.
/// </summary>
public class UrlValidationException : InvalidOperationException
{
    public UrlValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Custom exception for resource not found.
/// </summary>
public class NotFoundException : InvalidOperationException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// Custom exception for when short code collision retries are exhausted.
/// </summary>
public class CollisionExhaustionException : InvalidOperationException
{
    public CollisionExhaustionException(string message) : base(message)
    {
    }
}
