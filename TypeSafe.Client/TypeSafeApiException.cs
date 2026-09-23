using System.Net;

namespace TypeSafe.Client;

/// <summary>Exception thrown when the TypeSafe API returns an error response.</summary>
public class TypeSafeApiException : HttpRequestException
{
    /// <summary>The HTTP status code returned by the API.</summary>
    public new HttpStatusCode StatusCode { get; }

    /// <summary>The raw response body, when available.</summary>
    public string? ResponseBody { get; }

    /// <summary>True when the failure was 429 Too Many Requests.</summary>
    public bool IsRateLimited => StatusCode == (HttpStatusCode)429;

    /// <summary>True when the failure was 401 Unauthorized (missing or invalid API key).</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>True when the failure was 422 Unprocessable Entity (request validation failed).</summary>
    public bool IsValidationError => StatusCode == (HttpStatusCode)422;

    /// <summary>True when the failure was 529 (TypeSafe temporarily overloaded).</summary>
    public bool IsOverloaded => (int)StatusCode == 529;

    internal TypeSafeApiException(HttpStatusCode statusCode, string? responseBody)
        : base($"TypeSafe API request failed with status {(int)statusCode} ({statusCode}).{(string.IsNullOrWhiteSpace(responseBody) ? null : $" Body: {responseBody}")}",
            null, statusCode)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
