namespace TypeSafe.Client;

/// <summary>
/// Optional OpenJEV support. OpenJEV is a free community gateway to the same Jev model built by
/// TypeSafe. TypeSafe stays the default; OpenJEV is used only when explicitly chosen or when no
/// TypeSafe key is configured. Anyone with a TypeSafe key sees no behaviour change.
/// </summary>
public static class OpenJev
{
    /// <summary>Provider name for TypeSafe, the default.</summary>
    public const string TypeSafeProvider = "typesafe";

    /// <summary>Provider name for the OpenJEV community gateway.</summary>
    public const string OpenJevProvider = "openjev";

    /// <summary>Model id used by the OpenJEV gateway (the same Jev model, different gateway).</summary>
    public const string Model = TypeSafeModels.OpenJev;

    /// <summary>Environment variable for an explicit provider choice ("openjev" or "typesafe").</summary>
    public const string ProviderEnvVar = "JEV_PROVIDER";

    /// <summary>Environment variable for the OpenJEV API key.</summary>
    public const string ApiKeyEnvVar = "OPENJEV_API_KEY";

    /// <summary>
    /// Resolves the effective options following the selection rule:
    /// 1) an explicit provider wins — <c>JEV_PROVIDER</c> env var, <c>OpenJev:Provider</c> or
    ///    <c>TypeSafe:Provider</c> config ("openjev" or "typesafe");
    /// 2) otherwise TypeSafe when its key is set (the unchanged default);
    /// 3) otherwise OpenJEV when <c>OPENJEV_API_KEY</c> (env) or <c>OpenJev:ApiKey</c> (config) is set.
    /// TypeSafe is never hidden or removed; adjusted options are returned only when OpenJEV applies.
    /// </summary>
    public static TypeSafeClientOptions Resolve(TypeSafeClientOptions options, OpenJevOptions? openjev = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        openjev ??= new OpenJevOptions();

        var explicitProvider = (Environment.GetEnvironmentVariable(ProviderEnvVar)
            ?? openjev.Provider
            ?? options.Provider)?
            .Trim().ToLowerInvariant();

        var openjevKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar) ?? openjev.ApiKey;

        if (string.Equals(explicitProvider, OpenJevProvider, StringComparison.Ordinal))
            return WithOpenJev(options, openjev, openjevKey ?? options.ApiKey);

        if (string.Equals(explicitProvider, TypeSafeProvider, StringComparison.Ordinal))
            return options; // explicit TypeSafe; unchanged.

        // No explicit provider: keep TypeSafe when its key is set (default unchanged).
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            return options;

        // TypeSafe key absent: fall back to OpenJEV when its key is available.
        if (!string.IsNullOrWhiteSpace(openjevKey))
            return WithOpenJev(options, openjev, openjevKey);

        return options;
    }

    private static TypeSafeClientOptions WithOpenJev(TypeSafeClientOptions options, OpenJevOptions openjev, string apiKey)
    {
        // Derive the OpenJEV base address from the configured TypeSafe base address by swapping the
        // host, so no second endpoint is hard-coded and a custom TypeSafe BaseAddress is respected.
        var openjevBase = openjev.BaseAddress;
        if (string.IsNullOrWhiteSpace(openjevBase))
            openjevBase = options.BaseAddress.ToString().Replace("typesafe.ai", "openjev.sh");

        return new TypeSafeClientOptions
        {
            BaseAddress = new Uri(openjevBase!),
            ApiKey = apiKey,
            Model = string.IsNullOrWhiteSpace(openjev.Model) ? Model : openjev.Model!,
            MaxRetries = options.MaxRetries,
            InitialRetryDelay = options.InitialRetryDelay,
            Timeout = options.Timeout,
            Provider = OpenJevProvider,
        };
    }
}

/// <summary>Optional OpenJEV gateway configuration, bound from an "OpenJev" config section. All fields optional.</summary>
public sealed class OpenJevOptions
{
    /// <summary>Explicit provider choice: "openjev" or "typesafe". When set, overrides auto-detection.</summary>
    public string? Provider { get; set; }

    /// <summary>OpenJEV API key. Used when OpenJEV is active; falls back to the OPENJEV_API_KEY env var.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model id for OpenJEV. Defaults to "openjev".</summary>
    public string? Model { get; set; }

    /// <summary>OpenJEV base address. Defaults to the TypeSafe base address with the host swapped to openjev.sh.</summary>
    public string? BaseAddress { get; set; }
}
