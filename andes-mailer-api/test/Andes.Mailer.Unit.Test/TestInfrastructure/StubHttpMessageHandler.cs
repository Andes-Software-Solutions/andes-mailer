using System.Net;
using System.Text;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// Answers every request with one JSON body and records the request, so Graph SDK calls can be tested without a tenant.
public sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
{
    private readonly string _json = json;

    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json"),
        });
    }
}
