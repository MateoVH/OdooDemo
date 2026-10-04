using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using OdooConnector.Invoices;
using OdooConnector.Resources;
using OdooConnector.Tests.Infrastructure;

namespace OdooConnector.Tests;

public sealed partial class LocalizationTests
{
    private static readonly CultureInfo Spanish = new("es");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> MessageNames => new(Names());

    [Theory]
    [MemberData(nameof(MessageNames))]
    public void EveryMessageHasAnEnglishAndASpanishVersionWithTheSamePlaceholders(string name)
    {
        var english = Strings.ResourceManager.GetString(name, CultureInfo.InvariantCulture);
        var spanish = SpanishResources().GetString(name);

        Assert.False(string.IsNullOrWhiteSpace(english), $"'{name}' has no English text.");
        Assert.False(string.IsNullOrWhiteSpace(spanish), $"'{name}' has no Spanish text.");
        Assert.NotEqual(english, spanish);
        Assert.Equal(Placeholders(english!), Placeholders(spanish!));
    }

    [Fact]
    public void ThereAreNoUnusedTranslations()
    {
        var used = Names().ToHashSet();
        var translated = SpanishResources().Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key);

        Assert.DoesNotContain(translated, name => !used.Contains(name));
    }

    [Theory]
    [InlineData("es-CO", "Odoo rechazó las credenciales del usuario 'api@example.com' en la base de datos 'demo'. Revisa el usuario y la clave API.")]
    [InlineData("es-ES", "Odoo rechazó las credenciales del usuario 'api@example.com' en la base de datos 'demo'. Revisa el usuario y la clave API.")]
    [InlineData("en-US", "Odoo rejected the credentials of user 'api@example.com' on database 'demo'. Check the username and the API key.")]
    [InlineData("fr-FR", "Odoo rejected the credentials of user 'api@example.com' on database 'demo'. Check the username and the API key.")]
    public async Task MessagesFollowTheUiCulture(string culture, string expected)
    {
        var odoo = new FakeOdooServer { AuthenticateResult = false };

        var exception = await InCulture(culture, () =>
            Assert.ThrowsAsync<OdooAuthenticationException>(() => odoo.CreateClient().AuthenticateAsync(Ct)));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task ValidationErrorsAreTranslatedToo()
    {
        var invoices = new InvoiceService(new FakeOdooServer().CreateClient());

        var exception = await InCulture("es-MX", () =>
            Assert.ThrowsAsync<ArgumentException>(() => invoices.CreateAsync(new InvoiceDraft { PartnerId = 14, Lines = [] }, Ct)));

        Assert.StartsWith("Una factura necesita al menos una línea.", exception.Message, StringComparison.Ordinal);
    }

    // Every public member of Strings is a message whose resource name is the member name.
    private static IEnumerable<string> Names() =>
        typeof(Strings)
            .GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Where(member => member is PropertyInfo or MethodInfo { IsSpecialName: false })
            .Select(member => member.Name)
            .Distinct();

    private static ResourceSet SpanishResources() =>
        Strings.ResourceManager.GetResourceSet(Spanish, createIfNotExists: true, tryParents: false)
        ?? throw new InvalidOperationException("The Spanish satellite assembly was not found.");

    private static string[] Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal).ToArray();

    private static async Task<T> InCulture<T>(string culture, Func<Task<T>> action)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
