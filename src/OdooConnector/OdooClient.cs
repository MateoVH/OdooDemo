using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OdooConnector.Resources;
using OdooConnector.XmlRpc;

namespace OdooConnector;

/// <summary>
/// Client for Odoo's external XML-RPC API (<c>/xmlrpc/2/common</c> and <c>/xmlrpc/2/object</c>).
/// </summary>
/// <remarks>
/// The client authenticates on the first call and caches the user id, so reuse one instance (it is thread-safe).
/// With dependency injection use <c>services.AddOdooConnector(...)</c>, which registers it as a singleton.
/// </remarks>
public sealed partial class OdooClient : IOdooClient
{
    /// <summary>Name of the <see cref="HttpClient"/> registered by <c>AddOdooConnector</c>.</summary>
    public const string HttpClientName = "OdooConnector";

    private static readonly IReadOnlyDictionary<string, object?> EmptyStruct = new Dictionary<string, object?>();
    private static readonly string UserAgent = $"OdooConnector/{typeof(OdooClient).Assembly.GetName().Version?.ToString(3)}";

    private readonly Func<HttpClient> _httpClientAccessor;
    private readonly Uri _commonEndpoint;
    private readonly Uri _objectEndpoint;
    private readonly string _database;
    private readonly string _username;
    private readonly string _apiKey;
    private readonly ILogger _logger;
    private Lazy<Task<int>> _authentication;

    /// <summary>Creates a client that sends its requests through <paramref name="httpClient"/>.</summary>
    /// <param name="httpClient">HTTP client used for every call; the caller owns its lifetime.</param>
    /// <param name="options">Connection settings.</param>
    /// <param name="logger">Optional logger; calls are logged at <see cref="LogLevel.Debug"/> level.</param>
    /// <exception cref="ArgumentException"><paramref name="options"/> is incomplete or invalid.</exception>
    public OdooClient(HttpClient httpClient, OdooOptions options, ILogger<OdooClient>? logger = null)
        : this(CreateAccessor(httpClient), options, logger)
    {
    }

    internal OdooClient(Func<HttpClient> httpClientAccessor, OdooOptions options, ILogger<OdooClient>? logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientAccessor);
        ArgumentNullException.ThrowIfNull(options);

        var errors = OdooOptionsValidator.GetErrors(options);
        if (errors.Count > 0)
        {
            throw new ArgumentException(Strings.InvalidOptions(string.Join(" ", errors)), nameof(options));
        }

        // Keep any path prefix (e.g. a reverse proxy serving Odoo under /odoo/) when building the endpoints.
        var root = options.Url!.AbsoluteUri.EndsWith('/') ? options.Url : new Uri(options.Url.AbsoluteUri + "/");
        _commonEndpoint = new Uri(root, "xmlrpc/2/common");
        _objectEndpoint = new Uri(root, "xmlrpc/2/object");
        _database = options.Database;
        _username = options.Username;
        _apiKey = options.ApiKey;
        _httpClientAccessor = httpClientAccessor;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _authentication = new Lazy<Task<int>>(AuthenticateCoreAsync);
    }

    /// <inheritdoc/>
    public async Task<OdooVersionInfo> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallAsync(_commonEndpoint, "version", [], cancellationToken).ConfigureAwait(false);
        return OdooVersionInfo.FromRpc(result);
    }

    /// <inheritdoc/>
    public async Task<int> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var authentication = Volatile.Read(ref _authentication);
        try
        {
            // Concurrent callers share a single request, and once it succeeds the uid is reused for good.
            return await authentication.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (authentication.Value.IsFaulted)
        {
            // Don't cache failures: the next call tries again (e.g. after a network error or a corrected API key).
            Interlocked.CompareExchange(ref _authentication, new Lazy<Task<int>>(AuthenticateCoreAsync), authentication);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<object?> ExecuteKwAsync(
        string model,
        string method,
        IReadOnlyList<object?> args,
        IReadOnlyDictionary<string, object?>? kwargs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(args);

        var uid = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();

        // execute_kw(db, uid, password_or_api_key, model, method, args, kwargs)
        var result = await CallAsync(
                _objectEndpoint, "execute_kw", [_database, uid, _apiKey, model, method, args, kwargs ?? EmptyStruct], cancellationToken)
            .ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Log.CallCompleted(_logger, model, method, elapsedMilliseconds);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OdooRecord>> SearchReadAsync(
        string model,
        OdooDomain? domain = null,
        IEnumerable<string>? fields = null,
        int? limit = null,
        int? offset = null,
        string? order = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit.Value, nameof(limit));
        }

        if (offset is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset.Value, nameof(offset));
        }

        var kwargs = new Dictionary<string, object?>();
        if (fields is not null)
        {
            kwargs["fields"] = fields.ToArray();
        }

        if (limit is not null)
        {
            kwargs["limit"] = limit.Value;
        }

        if (offset is > 0)
        {
            kwargs["offset"] = offset.Value;
        }

        if (!string.IsNullOrWhiteSpace(order))
        {
            kwargs["order"] = order;
        }

        var result = await ExecuteKwAsync(model, "search_read", [domain ?? OdooDomain.Empty], kwargs, cancellationToken)
            .ConfigureAwait(false);
        return ToRecords(result, model, "search_read");
    }

    /// <inheritdoc/>
    public async Task<int> SearchCountAsync(string model, OdooDomain? domain = null, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteKwAsync(model, "search_count", [domain ?? OdooDomain.Empty], null, cancellationToken)
            .ConfigureAwait(false);
        return result as int? ?? throw UnexpectedResult(model, "search_count", result);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OdooRecord>> ReadAsync(
        string model,
        IEnumerable<int> ids,
        IEnumerable<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        var kwargs = fields is null ? null : new Dictionary<string, object?> { ["fields"] = fields.ToArray() };
        var result = await ExecuteKwAsync(model, "read", [idList], kwargs, cancellationToken).ConfigureAwait(false);
        return ToRecords(result, model, "read");
    }

    /// <inheritdoc/>
    public async Task<int> CreateAsync(
        string model,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = await ExecuteKwAsync(model, "create", [values], null, cancellationToken).ConfigureAwait(false);
        return result as int? ?? throw UnexpectedResult(model, "create", result);
    }

    /// <inheritdoc/>
    public async Task WriteAsync(
        string model,
        IEnumerable<int> ids,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(values);

        var idList = ids.ToArray();
        if (idList.Length > 0)
        {
            await ExecuteKwAsync(model, "write", [idList, values], null, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task UnlinkAsync(string model, IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToArray();
        if (idList.Length > 0)
        {
            await ExecuteKwAsync(model, "unlink", [idList], null, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Func<HttpClient> CreateAccessor(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return () => httpClient;
    }

    private async Task<int> AuthenticateCoreAsync()
    {
        // Not tied to a caller's token: other callers may be waiting on this same request.
        // authenticate(db, login, password_or_api_key, user_agent_env) returns the uid, or False.
        var result = await CallAsync(
                _commonEndpoint, "authenticate", [_database, _username, _apiKey, EmptyStruct], CancellationToken.None)
            .ConfigureAwait(false);

        if (result is not int uid || uid <= 0)
        {
            throw new OdooAuthenticationException(Strings.AuthenticationRejected(_username, _database));
        }

        Log.Authenticated(_logger, _database, uid);
        return uid;
    }

    private async Task<object?> CallAsync(
        Uri endpoint, string method, IReadOnlyList<object?> parameters, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new ByteArrayContent(XmlRpcSerializer.SerializeMethodCall(method, parameters));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        HttpResponseMessage response;
        try
        {
            response = await _httpClientAccessor().SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new OdooException(Strings.ServerUnreachable(endpoint, exception.Message), exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OdooException(Strings.RequestTimedOut(endpoint), exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new OdooException(response.StatusCode == HttpStatusCode.NotFound
                    ? Strings.HttpNotFound(endpoint)
                    : Strings.HttpError((int)response.StatusCode, response.StatusCode.ToString(), endpoint));
            }

            XmlRpcResponse rpcResponse;
            try
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    rpcResponse = await XmlRpcDeserializer.ReadMethodResponseAsync(stream, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is XmlException or FormatException)
            {
                throw new OdooException(Strings.InvalidResponse(endpoint), exception);
            }

            if (rpcResponse.Fault is { } fault)
            {
                throw ToException(fault);
            }

            return rpcResponse.Value;
        }
    }

    private static OdooException ToException(XmlRpcFault fault)
    {
        var exception = new OdooFaultException(fault.Code, fault.Message);
        return exception.Kind == OdooFaultKind.AccessDenied
            ? new OdooAuthenticationException(Strings.AccessDenied(exception.Message), exception)
            : exception;
    }

    private static OdooRecord[] ToRecords(object? result, string model, string method)
    {
        if (result is not IReadOnlyList<object?> rows)
        {
            throw UnexpectedResult(model, method, result);
        }

        var records = new OdooRecord[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            records[i] = rows[i] is IReadOnlyDictionary<string, object?> fields
                ? new OdooRecord(fields)
                : throw UnexpectedResult(model, method, rows[i]);
        }

        return records;
    }

    private static OdooException UnexpectedResult(string model, string method, object? result) =>
        new(Strings.UnexpectedResult(model, method, result?.GetType().Name ?? "null"));

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Authenticated on Odoo database {Database} as uid {Uid}")]
        public static partial void Authenticated(ILogger logger, string database, int uid);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "{Model}.{Method} completed in {ElapsedMilliseconds} ms")]
        public static partial void CallCompleted(ILogger logger, string model, string method, long elapsedMilliseconds);
    }
}
