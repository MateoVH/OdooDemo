using OdooConnector.Contacts;
using OdooConnector.Tests.Infrastructure;

namespace OdooConnector.Tests.Contacts;

public sealed class ContactServiceTests
{
    private const string Fields =
        "['name', 'display_name', 'is_company', 'email', 'phone', 'vat', 'street', 'city', 'zip', 'country_id', 'parent_id']";

    private readonly FakeOdooServer _odoo = new();
    private readonly ContactService _contacts;

    public ContactServiceTests() => _contacts = new ContactService(_odoo.CreateClient());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SearchAsync_TranslatesTheQueryIntoADomain()
    {
        _odoo.On("res.partner", "search_read", Array.Empty<object>());

        await _contacts.SearchAsync(
            new ContactQuery { Search = " acme ", IsCompany = true, CountryCode = "co", Limit = 10, Offset = 20, OrderBy = "name asc" },
            Ct);

        var call = _odoo.ModelCalls.Single();
        Assert.Equal("search_read", call.Method);
        Assert.Equal(
            "[['&', '&', '|', ['name', 'ilike', 'acme'], ['email', 'ilike', 'acme'], ['is_company', '=', True], ['country_id.code', '=', 'CO']]]",
            Py.Repr(call.Args));
        Assert.Equal($"{{'fields': {Fields}, 'limit': 10, 'offset': 20, 'order': 'name asc'}}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task SearchAsync_ReturnsTheFirst100ActiveContactsByDefault()
    {
        _odoo.On("res.partner", "search_read", Array.Empty<object>());

        await _contacts.SearchAsync(cancellationToken: Ct);

        var call = _odoo.ModelCalls.Single();
        Assert.Equal("[[]]", Py.Repr(call.Args));
        Assert.Equal($"{{'fields': {Fields}, 'limit': 100}}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task SearchAsync_MapsTheRecordsReturnedByOdoo()
    {
        _odoo.OnRaw("res.partner", "search_read", PythonFixtures.PartnersSearchRead);

        var contacts = await _contacts.SearchAsync(cancellationToken: Ct);

        Assert.Equal(2, contacts.Count);
        Assert.Equal(
            new Contact
            {
                Id = 14,
                Name = "Azure Interior",
                IsCompany = true,
                Email = "azure.interior24@example.com",
                Phone = "(870)-931-0505",
                Street = "4557 De Silva St",
                City = "Fremont",
                Zip = "94538",
                Country = new OdooReference(233, "United States"),
            },
            contacts[0]);

        // Odoo sends False for empty fields; they become null instead of the string "False".
        var colombian = contacts[1];
        Assert.Equal("Ñandú & Cía <S.A.S>", colombian.Name);
        Assert.Equal("900123456-7", colombian.TaxId);
        Assert.Null(colombian.Email);
        Assert.Null(colombian.Street);
        Assert.Null(colombian.ParentCompany);
        Assert.Equal(new OdooReference(49, "Colombia"), colombian.Country);
    }

    [Fact]
    public async Task SearchAsync_UsesTheDisplayNameOfAddressesWithoutName()
    {
        _odoo.On("res.partner", "search_read", new object[]
        {
            Partner(30, name: false, displayName: "Azure Interior, Invoice Address", parent: new object[] { 14, "Azure Interior" }),
        });

        var contact = Assert.Single(await _contacts.SearchAsync(cancellationToken: Ct));

        Assert.Equal("Azure Interior, Invoice Address", contact.Name);
        Assert.Equal(new OdooReference(14, "Azure Interior"), contact.ParentCompany);
    }

    [Fact]
    public async Task SearchAsync_CombinesTheCustomFilterWithAnd()
    {
        _odoo.On("res.partner", "search_read", Array.Empty<object>());

        await _contacts.SearchAsync(
            new ContactQuery { IsCompany = false, Filter = OdooDomain.Where("customer_rank", ">", 0) },
            Ct);

        Assert.Equal(
            "[['&', ['is_company', '=', False], ['customer_rank', '>', 0]]]",
            Py.Repr(_odoo.ModelCalls.Single().Args));
    }

    [Fact]
    public async Task CountAsync_UsesTheSameFiltersWithoutPaging()
    {
        _odoo.On("res.partner", "search_count", 34);

        var count = await _contacts.CountAsync(new ContactQuery { IsCompany = true, Limit = 5, Offset = 10 }, Ct);

        Assert.Equal(34, count);
        var call = _odoo.ModelCalls.Single();
        Assert.Equal("[[['is_company', '=', True]]]", Py.Repr(call.Args));
        Assert.Equal("{}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task GetByIdAsync_FindsArchivedContactsToo()
    {
        _odoo.On("res.partner", "search_read", new object[] { Partner(7, name: "Archived Ltd") });

        var contact = await _contacts.GetByIdAsync(7, Ct);

        Assert.Equal("Archived Ltd", contact?.Name);
        var call = _odoo.ModelCalls.Single();
        Assert.Equal("[['&', ['id', '=', 7], ['active', 'in', [True, False]]]]", Py.Repr(call.Args));
        Assert.Equal($"{{'fields': {Fields}, 'limit': 1}}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenTheContactDoesNotExist()
    {
        _odoo.On("res.partner", "search_read", Array.Empty<object>());

        Assert.Null(await _contacts.GetByIdAsync(404, Ct));
    }

    [Fact]
    public async Task GetByIdAsync_RejectsInvalidIds()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _contacts.GetByIdAsync(0, Ct));
    }

    private static Dictionary<string, object?> Partner(int id, object name, object? displayName = null, object? parent = null) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["display_name"] = displayName ?? name,
        ["is_company"] = false,
        ["email"] = false,
        ["phone"] = false,
        ["vat"] = false,
        ["street"] = false,
        ["city"] = false,
        ["zip"] = false,
        ["country_id"] = false,
        ["parent_id"] = parent ?? false,
    };
}
