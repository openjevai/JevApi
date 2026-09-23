namespace TypeSafe.Client;

/// <summary>Options for the TypeSafe AI client.</summary>
public sealed class TypeSafeClientOptions
{
    /// <summary>Base address of the TypeSafe API. Defaults to https://api.typesafe.ai.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.typesafe.ai/");

    /// <summary>API key sent as a bearer token on every request.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model used for evaluations. Defaults to "jev-latest".</summary>
    public string Model { get; set; } = TypeSafeModels.JevLatest;

    /// <summary>Maximum number of retry attempts for 429 (rate limited) and 529 (overloaded) responses. Defaults to 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Initial backoff delay used for retries; grows exponentially per attempt. Defaults to 500 ms.</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Request timeout applied to each underlying HTTP attempt. Null uses the HttpClient default.</summary>
    public TimeSpan? Timeout { get; set; }
}

/// <summary>Well-known TypeSafe model names.</summary>
public static class TypeSafeModels
{
    /// <summary>Alias for TypeSafe's flagship model.</summary>
    public const string JevLatest = "jev-latest";
}
