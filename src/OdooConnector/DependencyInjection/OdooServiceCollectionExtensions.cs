using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OdooConnector;
using OdooConnector.Contacts;
using OdooConnector.Invoices;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the Odoo connector in an <see cref="IServiceCollection"/>.</summary>
public static class OdooServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOdooClient"/>, <see cref="IContactService"/> and <see cref="IInvoiceService"/>
    /// with settings bound from <paramref name="configuration"/> (usually the <c>"Odoo"</c> section).
    /// </summary>
    /// <returns>The builder of the underlying <see cref="HttpClient"/>, to add handlers, timeouts or resilience policies.</returns>
    public static IHttpClientBuilder AddOdooConnector(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OdooOptions>().Bind(configuration);
        return services.AddOdooConnectorCore();
    }

    /// <summary>
    /// Registers <see cref="IOdooClient"/>, <see cref="IContactService"/> and <see cref="IInvoiceService"/>
    /// with settings set by <paramref name="configure"/>.
    /// </summary>
    /// <returns>The builder of the underlying <see cref="HttpClient"/>, to add handlers, timeouts or resilience policies.</returns>
    public static IHttpClientBuilder AddOdooConnector(this IServiceCollection services, Action<OdooOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<OdooOptions>().Configure(configure);
        return services.AddOdooConnectorCore();
    }

    private static IHttpClientBuilder AddOdooConnectorCore(this IServiceCollection services)
    {
        services.AddOptions<OdooOptions>().ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OdooOptions>, OdooOptionsValidator>());

        // A singleton keeps the authenticated uid; it asks the factory for an HttpClient on every call,
        // so the pooled handlers are still rotated as IHttpClientFactory intends.
        services.TryAddSingleton<IOdooClient>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new OdooClient(
                () => factory.CreateClient(OdooClient.HttpClientName),
                provider.GetRequiredService<IOptions<OdooOptions>>().Value,
                provider.GetService<ILogger<OdooClient>>());
        });
        services.TryAddSingleton<IContactService, ContactService>();
        services.TryAddSingleton<IInvoiceService, InvoiceService>();

        return services.AddHttpClient(OdooClient.HttpClientName);
    }
}
