using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OdooConnector.Contacts;
using OdooConnector.Invoices;
using OdooConnector.Tests.Infrastructure;

namespace OdooConnector.Tests;

public sealed class DependencyInjectionTests
{
    private readonly FakeOdooServer _odoo = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddOdooConnector_BindsTheConfigurationAndTalksToOdoo()
    {
        _odoo.On("res.partner", "search_read", Array.Empty<object>());
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Odoo:Url"] = "https://odoo.test",
            ["Odoo:Database"] = "demo",
            ["Odoo:Username"] = "api@example.com",
            ["Odoo:ApiKey"] = "test-api-key",
        });

        var contacts = provider.GetRequiredService<IContactService>();
        await contacts.SearchAsync(cancellationToken: Ct);

        Assert.NotNull(provider.GetRequiredService<IInvoiceService>());
        Assert.Equal(new[] { "authenticate", "execute_kw" }, _odoo.Calls.Select(call => call.MethodName));
        Assert.Equal("['demo', 'api@example.com', 'test-api-key', {}]", Py.Repr(_odoo.Calls[0].Params));
    }

    [Fact]
    public async Task AddOdooConnector_SharesOneAuthenticatedClient()
    {
        _odoo.On("res.partner", "search_count", 1);
        var services = new ServiceCollection();
        services.AddOdooConnector(options =>
            {
                options.Url = new Uri("https://odoo.test");
                options.Database = "demo";
                options.Username = "api@example.com";
                options.ApiKey = "test-api-key";
            })
            .ConfigurePrimaryHttpMessageHandler(() => _odoo);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IOdooClient>();
        var second = provider.GetRequiredService<IOdooClient>();
        await first.SearchCountAsync("res.partner", cancellationToken: Ct);
        await second.SearchCountAsync("res.partner", cancellationToken: Ct);

        Assert.Same(first, second);
        Assert.Single(_odoo.Calls, call => call.MethodName == "authenticate");
    }

    [Fact]
    public void AddOdooConnector_ReportsIncompleteConfiguration()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Odoo:Url"] = "https://odoo.test",
            ["Odoo:Database"] = "demo",
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOdooClient>());

        Assert.Contains(exception.Failures, failure => failure.Contains("Odoo:Username", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("Odoo:ApiKey", StringComparison.Ordinal));
    }

    private ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddOdooConnector(configuration.GetSection(OdooOptions.SectionName))
            .ConfigurePrimaryHttpMessageHandler(() => _odoo);
        return services.BuildServiceProvider();
    }
}
