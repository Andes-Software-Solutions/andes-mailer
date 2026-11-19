namespace Andes.Mailer.Unit.Test.TestInfrastructure;

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body);
