using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TypeSafe.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration helpers for <see cref="TypeSafeClient"/>.</summary>
public static class TypeSafeServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TypeSafeClient"/> as a typed <see cref="HttpClient"/> and configures it
    /// with the given options.
    /// </summary>
    public static IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services, Action<TypeSafeClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<TypeSafeClientOptions>().Configure(configure);

        return services.AddHttpClient<TypeSafeClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TypeSafeClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            if (options.Timeout is { } timeout)
                client.Timeout = timeout;
        });
    }

    /// <summary>
    /// Registers <see cref="TypeSafeClient"/> as a typed <see cref="HttpClient"/> and binds options
    /// from the given configuration section (expects at least an "ApiKey" value).
    /// </summary>
    public static IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<TypeSafeClientOptions>().Bind(configuration);

        return services.AddHttpClient<TypeSafeClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TypeSafeClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            if (options.Timeout is { } timeout)
                client.Timeout = timeout;
        });
    }
}
