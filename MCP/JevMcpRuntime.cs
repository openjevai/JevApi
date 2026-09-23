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

        if (string.IsNullOrWhiteSpace(typeSafeOptions.ApiKey))
            Console.Error.WriteLine("JevMcp: no API key configured. Set TypeSafe:ApiKey in appsettings.json or the TYPESAFE__ApiKey environment variable; tool calls will return 'unauthorized'.");

        var httpClient = new HttpClient { BaseAddress = typeSafeOptions.BaseAddress };
        if (typeSafeOptions.Timeout is { } timeout)
            httpClient.Timeout = timeout;

        var client = new TypeSafeClient(httpClient, Microsoft.Extensions.Options.Options.Create(typeSafeOptions));
        return new JevEvaluator(client, mcpOptions);
    }
}
