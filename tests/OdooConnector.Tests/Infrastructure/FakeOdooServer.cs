using System.Net;
using System.Text;

namespace OdooConnector.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for an Odoo server. It decodes the XML-RPC requests sent by the connector,
/// records them and answers with configurable results or faults.
/// </summary>
internal sealed class FakeOdooServer : HttpMessageHandler
{
    public const int Uid = 2;

    private readonly object _gate = new();
    private readonly List<RpcCall> _calls = [];
    private readonly Dictionary<(string Model, string Method), Func<RpcCall, object?>> _handlers = [];

    /// <summary>Value returned by <c>common.authenticate</c>: a uid, or <c>false</c> to reject the credentials.</summary>
    public object? AuthenticateResult { get; set; } = Uid;

    /// <summary>Delay before answering <c>common.authenticate</c>, to exercise concurrent callers.</summary>
    public TimeSpan AuthenticateDelay { get; set; }

    public Dictionary<string, object?> VersionInfo { get; } = new()
    {
        ["server_version"] = "17.0+e",
        ["server_version_info"] = new object[] { 17, 0, 0, "final", 0, "e" },
        ["server_serie"] = "17.0",
        ["protocol_version"] = 1,
    };

    public IReadOnlyList<RpcCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The <c>execute_kw</c> calls (model methods), in order.</summary>
    public IReadOnlyList<RpcCall> ModelCalls => Calls.Where(call => call.MethodName == "execute_kw").ToArray();

    public static OdooOptions CreateOptions(string url = "https://odoo.test") => new()
    {
        Url = new Uri(url),
        Database = "demo",
        Username = "api@example.com",
        ApiKey = "test-api-key",
    };

    public OdooClient CreateClient(OdooOptions? options = null) =>
        new(new HttpClient(this, disposeHandler: false), options ?? CreateOptions());

    /// <summary>Answers <paramref name="model"/>.<paramref name="method"/> with the value returned by <paramref name="handler"/>.</summary>
    public FakeOdooServer On(string model, string method, Func<RpcCall, object?> handler)
    {
        lock (_gate)
        {
            _handlers[(model, method)] = handler;
        }

        return this;
    }

    public FakeOdooServer On(string model, string method, object? result) => On(model, method, _ => result);

    /// <summary>Answers with a raw XML-RPC document, e.g. one written by Python's xmlrpc module.</summary>
    public FakeOdooServer OnRaw(string model, string method, string xml) => On(model, method, _ => new RawXml(xml));

    public FakeOdooServer OnFault(string model, string method, int code, string message) =>
        On(model, method, _ => throw new FaultException(code, message));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = RpcCall.Parse(request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken));
        lock (_gate)
        {
            _calls.Add(call);
        }

        string body;
        try
        {
            body = await DispatchAsync(call, cancellationToken) switch
            {
                RawXml raw => raw.Xml,
                var result => XmlRpcDocuments.Response(result),
            };
        }
        catch (FaultException fault)
        {
            body = XmlRpcDocuments.Fault(fault.Code, fault.Message);
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
    }

    private async Task<object?> DispatchAsync(RpcCall call, CancellationToken cancellationToken)
    {
        switch (call.Service, call.MethodName)
        {
            case ("common", "version"):
                return VersionInfo;

            case ("common", "authenticate"):
                await Task.Delay(AuthenticateDelay, cancellationToken);
                return AuthenticateResult;

            case ("object", "execute_kw"):
                Func<RpcCall, object?>? handler;
                lock (_gate)
                {
                    _handlers.TryGetValue((call.Model, call.Method), out handler);
                }

                return handler is not null
                    ? handler(call)
                    : throw new FaultException(1, $"Traceback (most recent call last):\nAttributeError: no fake handler for {call.Model}.{call.Method}");

            default:
                throw new FaultException(1, $"Unknown endpoint {call.Path} ({call.MethodName})");
        }
    }

    private sealed record RawXml(string Xml);

    private sealed class FaultException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }
}
