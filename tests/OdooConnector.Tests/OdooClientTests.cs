using System.Net;
using System.Xml;
using OdooConnector.Tests.Infrastructure;

namespace OdooConnector.Tests;

public sealed class OdooClientTests
{
    private readonly FakeOdooServer _odoo = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetVersionAsync_ReadsTheVersionWithoutAuthenticating()
    {
        var version = await _odoo.CreateClient().GetVersionAsync(Ct);

        Assert.Equal(new OdooVersionInfo("17.0+e", "17.0", 1), version);
        Assert.Equal(17, version.MajorVersion);

        var call = Assert.Single(_odoo.Calls);
        Assert.Equal("/xmlrpc/2/common", call.Path);
        Assert.Equal("version", call.MethodName);
    }

    [Fact]
    public async Task AuthenticateAsync_SendsTheCredentialsToTheCommonEndpoint()
    {
        var uid = await _odoo.CreateClient().AuthenticateAsync(Ct);

        Assert.Equal(FakeOdooServer.Uid, uid);
        var call = Assert.Single(_odoo.Calls);
        Assert.Equal("/xmlrpc/2/common", call.Path);
        Assert.Equal("authenticate", call.MethodName);
        Assert.Equal("['demo', 'api@example.com', 'test-api-key', {}]", Py.Repr(call.Params));
    }

    [Fact]
    public async Task AuthenticateAsync_ContactsOdooOnlyOnce()
    {
        var client = _odoo.CreateClient();

        await client.AuthenticateAsync(Ct);
        await client.AuthenticateAsync(Ct);

        Assert.Single(_odoo.Calls);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectedCredentials_ThrowsWithoutRevealingTheApiKey()
    {
        _odoo.AuthenticateResult = false;

        var exception = await Assert.ThrowsAsync<OdooAuthenticationException>(() => _odoo.CreateClient().AuthenticateAsync(Ct));

        Assert.Contains("api@example.com", exception.Message, StringComparison.Ordinal);
        Assert.Contains("demo", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-api-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_DoesNotCacheFailures()
    {
        var client = _odoo.CreateClient();
        _odoo.AuthenticateResult = false;
        await Assert.ThrowsAsync<OdooAuthenticationException>(() => client.AuthenticateAsync(Ct));

        _odoo.AuthenticateResult = FakeOdooServer.Uid;

        Assert.Equal(FakeOdooServer.Uid, await client.AuthenticateAsync(Ct));
    }

    [Fact]
    public async Task ConcurrentCalls_ShareASingleAuthentication()
    {
        _odoo.AuthenticateDelay = TimeSpan.FromMilliseconds(100);
        _odoo.On("res.partner", "search_count", 3);
        var client = _odoo.CreateClient();

        var counts = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.SearchCountAsync("res.partner", cancellationToken: Ct)));

        Assert.All(counts, count => Assert.Equal(3, count));
        Assert.Single(_odoo.Calls, call => call.MethodName == "authenticate");
    }

    [Fact]
    public async Task ExecuteKwAsync_SendsTheStandardEnvelope()
    {
        _odoo.On("res.partner", "check_access_rights", true);

        var result = await _odoo.CreateClient().ExecuteKwAsync(
            "res.partner",
            "check_access_rights",
            ["write"],
            new Dictionary<string, object?> { ["raise_exception"] = false },
            Ct);

        Assert.Equal(true, result);
        var call = _odoo.Calls[^1];
        Assert.Equal("/xmlrpc/2/object", call.Path);
        Assert.Equal("execute_kw", call.MethodName);
        Assert.Equal(
            "['demo', 2, 'test-api-key', 'res.partner', 'check_access_rights', ['write'], {'raise_exception': False}]",
            Py.Repr(call.Params));
    }

    [Theory]
    [InlineData("http://localhost:8069", "/xmlrpc/2/object")]
    [InlineData("https://erp.example.com/odoo", "/odoo/xmlrpc/2/object")]
    [InlineData("https://erp.example.com/odoo/", "/odoo/xmlrpc/2/object")]
    public async Task KeepsThePathOfTheConfiguredUrl(string url, string expectedPath)
    {
        _odoo.On("res.partner", "search_count", 0);

        await _odoo.CreateClient(FakeOdooServer.CreateOptions(url)).SearchCountAsync("res.partner", cancellationToken: Ct);

        Assert.Equal(expectedPath, _odoo.Calls[^1].Path);
    }

    [Fact]
    public async Task SearchReadAsync_SendsDomainFieldsAndPaging()
    {
        _odoo.On("res.partner", "search_read", new object[]
        {
            new Dictionary<string, object?> { ["id"] = 14, ["name"] = "Azure Interior" },
        });

        var records = await _odoo.CreateClient().SearchReadAsync(
            "res.partner",
            OdooDomain.Where("is_company", "=", true),
            fields: ["name"],
            limit: 5,
            offset: 10,
            order: "name asc",
            cancellationToken: Ct);

        var record = Assert.Single(records);
        Assert.Equal(14, record.Id);
        Assert.Equal("Azure Interior", record.GetString("name"));

        var call = _odoo.ModelCalls.Single();
        Assert.Equal("[[['is_company', '=', True]]]", Py.Repr(call.Args));
        Assert.Equal("{'fields': ['name'], 'limit': 5, 'offset': 10, 'order': 'name asc'}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task SearchReadAsync_LeavesUnsetOptionsToOdoo()
    {
        _odoo.On("res.partner", "search_read", Array.Empty<object>());

        var records = await _odoo.CreateClient().SearchReadAsync("res.partner", cancellationToken: Ct);

        Assert.Empty(records);
        var call = _odoo.ModelCalls.Single();
        Assert.Equal("[[]]", Py.Repr(call.Args));
        Assert.Equal("{}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task SearchReadAsync_RejectsInvalidPaging()
    {
        var client = _odoo.CreateClient();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchReadAsync("res.partner", limit: 0, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchReadAsync("res.partner", offset: -1, cancellationToken: Ct));
        Assert.Empty(_odoo.Calls);
    }

    [Fact]
    public async Task ReadAsync_SendsIdsAndFields()
    {
        _odoo.On("account.move.line", "read", new object[] { new Dictionary<string, object?> { ["id"] = 5, ["name"] = "Consulting" } });

        var records = await _odoo.CreateClient().ReadAsync("account.move.line", [5], ["name"], Ct);

        Assert.Equal("Consulting", Assert.Single(records).GetString("name"));
        var call = _odoo.ModelCalls.Single();
        Assert.Equal("[[5]]", Py.Repr(call.Args));
        Assert.Equal("{'fields': ['name']}", Py.Repr(call.Kwargs));
    }

    [Fact]
    public async Task ReadWriteAndUnlink_DoNotCallOdooWithoutIds()
    {
        var client = _odoo.CreateClient();

        Assert.Empty(await client.ReadAsync("res.partner", [], cancellationToken: Ct));
        await client.WriteAsync("res.partner", [], new Dictionary<string, object?> { ["name"] = "x" }, Ct);
        await client.UnlinkAsync("res.partner", [], Ct);

        Assert.Empty(_odoo.Calls);
    }

    [Fact]
    public async Task CreateWriteAndUnlink_UseTheOrmMethods()
    {
        _odoo.On("res.partner", "create", 42).On("res.partner", "write", true).On("res.partner", "unlink", true);
        var client = _odoo.CreateClient();

        var id = await client.CreateAsync("res.partner", new Dictionary<string, object?> { ["name"] = "Acme", ["is_company"] = true }, Ct);
        await client.WriteAsync("res.partner", [id], new Dictionary<string, object?> { ["email"] = "info@acme.test" }, Ct);
        await client.UnlinkAsync("res.partner", [id], Ct);

        Assert.Equal(42, id);
        Assert.Equal(
            new[] { "[{'name': 'Acme', 'is_company': True}]", "[[42], {'email': 'info@acme.test'}]", "[[42]]" },
            _odoo.ModelCalls.Select(call => Py.Repr(call.Args)));
    }

    [Fact]
    public async Task UserErrors_BecomeFaultExceptionsWithTheMessageForUsers()
    {
        _odoo.OnFault("account.move", "action_post", 2, "The field 'Customer' is required to post the invoice.");

        var exception = await Assert.ThrowsAsync<OdooFaultException>(
            () => _odoo.CreateClient().ExecuteKwAsync("account.move", "action_post", [new[] { 1 }], cancellationToken: Ct));

        Assert.Equal(OdooFaultKind.UserError, exception.Kind);
        Assert.Equal(2, exception.FaultCode);
        Assert.Equal("The field 'Customer' is required to post the invoice.", exception.Message);
    }

    [Fact]
    public async Task ServerErrors_SummarizeTheTracebackButKeepIt()
    {
        var client = StubHttpHandler.Returns(HttpStatusCode.OK, PythonFixtures.ServerErrorFault).CreateClient();

        var exception = await Assert.ThrowsAsync<OdooFaultException>(() => client.GetVersionAsync(Ct));

        Assert.Equal(OdooFaultKind.ServerError, exception.Kind);
        Assert.Equal("ValueError: Invalid field 'mobile_phone' on model 'res.partner'", exception.Message);
        Assert.StartsWith("Traceback (most recent call last):", exception.FaultString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccessErrors_AreReportedAsSuch()
    {
        _odoo.OnFault("account.move", "create", 4, "You are not allowed to create 'Journal Entry' (account.move) records.");

        var exception = await Assert.ThrowsAsync<OdooFaultException>(
            () => _odoo.CreateClient().CreateAsync("account.move", new Dictionary<string, object?>(), Ct));

        Assert.Equal(OdooFaultKind.AccessError, exception.Kind);
    }

    [Fact]
    public async Task AccessDenied_BecomesAnAuthenticationException()
    {
        _odoo.OnFault("res.partner", "search_count", 3, "Access Denied");

        var exception = await Assert.ThrowsAsync<OdooAuthenticationException>(
            () => _odoo.CreateClient().SearchCountAsync("res.partner", cancellationToken: Ct));

        Assert.Equal(OdooFaultKind.AccessDenied, Assert.IsType<OdooFaultException>(exception.InnerException).Kind);
    }

    [Fact]
    public async Task HttpErrors_IncludeTheStatusCode()
    {
        var client = StubHttpHandler.Returns(HttpStatusCode.BadGateway, "<html>502</html>", "text/html").CreateClient();

        var exception = await Assert.ThrowsAsync<OdooException>(() => client.GetVersionAsync(Ct));

        Assert.Contains("HTTP 502", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotFound_SuggestsCheckingTheUrl()
    {
        var client = StubHttpHandler.Returns(HttpStatusCode.NotFound, "Not Found", "text/plain").CreateClient();

        var exception = await Assert.ThrowsAsync<OdooException>(() => client.GetVersionAsync(Ct));

        Assert.Contains("root of the Odoo server", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponsesThatAreNotXmlRpc_AreReportedClearly()
    {
        var client = StubHttpHandler.Returns(HttpStatusCode.OK, "<html><body>Login</body></html>", "text/html").CreateClient();

        var exception = await Assert.ThrowsAsync<OdooException>(() => client.GetVersionAsync(Ct));

        Assert.Contains("not valid XML-RPC", exception.Message, StringComparison.Ordinal);
        Assert.IsType<FormatException>(exception.InnerException);
    }

    [Fact]
    public async Task MalformedXml_IsReportedClearly()
    {
        var client = StubHttpHandler.Returns(HttpStatusCode.OK, "<methodResponse><params>").CreateClient();

        var exception = await Assert.ThrowsAsync<OdooException>(() => client.GetVersionAsync(Ct));

        Assert.IsType<XmlException>(exception.InnerException);
    }

    [Fact]
    public async Task NetworkFailures_AreWrapped()
    {
        var client = StubHttpHandler.Throws(new HttpRequestException("No such host is known.")).CreateClient();

        var exception = await Assert.ThrowsAsync<OdooException>(() => client.GetVersionAsync(Ct));

        Assert.Contains("Could not reach Odoo", exception.Message, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task Timeouts_AreReportedAsSuch()
    {
        // HttpClient reports its own timeout as a TaskCanceledException while the caller's token is still active.
        var client = StubHttpHandler.Throws(new TaskCanceledException("timeout", new TimeoutException())).CreateClient();

        var exception = await Assert.ThrowsAsync<OdooException>(() => client.GetVersionAsync(Ct));

        Assert.Contains("timed out", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_IsNotWrapped()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _odoo.CreateClient().GetVersionAsync(cancellation.Token));
    }

    [Fact]
    public async Task LogsCallsWithoutTheApiKey()
    {
        _odoo.On("res.partner", "search_count", 3);
        var logger = new ListLogger<OdooClient>();
        var client = new OdooClient(new HttpClient(_odoo, disposeHandler: false), FakeOdooServer.CreateOptions(), logger);

        await client.SearchCountAsync("res.partner", cancellationToken: Ct);

        Assert.Contains(logger.Messages, message => message.StartsWith("res.partner.search_count completed in", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("uid 2", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("test-api-key", StringComparison.Ordinal));
    }

    public static TheoryData<string?, string, string, string, string> InvalidOptions => new()
    {
        { null, "demo", "user", "key", "Odoo:Url" },
        { "/relative", "demo", "user", "key", "absolute" },
        { "ftp://odoo.test", "demo", "user", "key", "absolute http(s)" },
        { "https://odoo.test/?db=demo", "demo", "user", "key", "query string" },
        { "https://odoo.test", " ", "user", "key", "Odoo:Database" },
        { "https://odoo.test", "demo", "", "key", "Odoo:Username" },
        { "https://odoo.test", "demo", "user", "", "Odoo:ApiKey" },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void RejectsInvalidOptions(string? url, string database, string username, string apiKey, string expectedError)
    {
        var options = new OdooOptions
        {
            Url = url is null ? null : new Uri(url, UriKind.RelativeOrAbsolute),
            Database = database,
            Username = username,
            ApiKey = apiKey,
        };

        var exception = Assert.Throws<ArgumentException>(() => new OdooClient(new HttpClient(), options));

        Assert.Contains(expectedError, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsToStringDoesNotRevealTheApiKey()
    {
        var text = FakeOdooServer.CreateOptions().ToString();

        Assert.Contains("api@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("test-api-key", text, StringComparison.Ordinal);
    }
}
