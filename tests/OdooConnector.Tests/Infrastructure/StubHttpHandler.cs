using System.Net;
using System.Text;

namespace OdooConnector.Tests.Infrastructure;

/// <summary>Answers every request with a fixed response or exception, to test transport failures.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public static StubHttpHandler Returns(HttpStatusCode status, string body, string mediaType = "text/xml") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) }));

    public static StubHttpHandler Throws(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    public OdooClient CreateClient() => new(new HttpClient(this, disposeHandler: false), FakeOdooServer.CreateOptions());

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        respond(request, cancellationToken);
}
