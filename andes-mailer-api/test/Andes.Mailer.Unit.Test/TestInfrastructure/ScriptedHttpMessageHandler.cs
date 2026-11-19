using System.Net;
using System.Text;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// Answers each request through a callback and records method, URI, and body, so a Graph SDK call sequence can be tested without a tenant.
public sealed class ScriptedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond = respond;

    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage Empty(HttpStatusCode status) => new(status);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

        return _respond(request);
    }
}
