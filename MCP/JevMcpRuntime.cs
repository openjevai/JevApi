using Microsoft.Extensions.Configuration;
using TypeSafe.Client;

namespace JevMcp;

/// <summary>
/// Lazily-built, process-wide evaluator for MCPSharp's parameterless tool construction.
/// Configuration is read once from appsettings.json next to the executable plus environment variables.
/// </summary>
internal static class JevMcpRuntime
{
    private static readonly Lazy<JevEvaluator> _evaluator = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    public static JevEvaluator Evaluator => _evaluator.Value;

    private static JevEvaluator Build()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var typeSafeOptions = new TypeSafeClientOptions();
        config.GetSection("TypeSafe").Bind(typeSafeOptions);
        var mcpOptions = new McpServerOptions();
        config.GetSection("Mcp").Bind(mcpOptions);

        var openjevOptions = new OpenJevOptions();
        config.GetSection("OpenJev").Bind(openjevOptions);

        // Optional OpenJEV (community gateway) support. TypeSafe stays the default; OpenJEV is used
        // only when explicitly requested (JEV_PROVIDER=openjev / OpenJev:Provider / TypeSafe:Provider)
        // or when no TypeSafe key is configured but an OpenJEV key is. See OPENJEV.md.
        typeSafeOptions = OpenJev.Resolve(typeSafeOptions, openjevOptions);

        if (string.IsNullOrWhiteSpace(typeSafeOptions.ApiKey))
            Console.Error.WriteLine("JevMcp: no API key configured. Set TypeSafe:ApiKey in appsettings.json, the TYPESAFE__ApiKey environment variable, or use OpenJEV (OPENJEV_API_KEY / JEV_PROVIDER=openjev); tool calls will return 'unauthorized'.");

        var httpClient = new HttpClient { BaseAddress = typeSafeOptions.BaseAddress };
        if (typeSafeOptions.Timeout is { } timeout)
            httpClient.Timeout = timeout;

        var client = new TypeSafeClient(httpClient, Microsoft.Extensions.Options.Options.Create(typeSafeOptions));
        return new JevEvaluator(client, mcpOptions, typeSafeOptions);
    }
}
