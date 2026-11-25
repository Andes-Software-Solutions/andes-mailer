using Andes.Mailer.Api.Configuration;
using Andes.Mailer.Api.Endpoints;
using Andes.Mailer.Api.Health;
using Andes.Mailer.Api.Middleware;
using Andes.Mailer.Api.Observability;
using Andes.Mailer.Api.Problems;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAndesMailerTelemetry(builder.Configuration);
builder.Services.AddAndesMailerProblemDetails();
builder.Services.AddAndesMailerOpenApi();
builder.Services.AddAndesMailerJson();
builder.Services.AddAndesMailerValidation();
builder.Services.AddAndesMailerSqlPersistence(builder.Configuration);
builder.Services.AddAndesMailerBlobPersistence(builder.Configuration);
builder.Services.AddAndesMailerHealth();
builder.Services.AddAndesMailerAuthentication(builder.Configuration);
builder.Services.AddAndesMailerRateLimiting(builder.Configuration);
builder.Services.AddAndesMailerIdempotency(builder.Configuration);
builder.Services.AddUsers();
builder.Services.AddTeams();
builder.Services.AddContactGroups();
builder.Services.AddEmails(builder.Configuration);

var app = builder.Build();

// First, ahead of the exception handler, so the request line logs the status the exception handler writes.
app.UseAndesMailerRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

// Explicit: the auto-added pair would run ahead of UseStatusCodePages and leave 401 and 403 with empty bodies.
app.UseAuthentication();

// Right after authentication, the first point the caller is known, so every later log and the request trace carry the oid.
app.UseAndesMailerRequestContext();
app.UseAuthorization();

// After authorization, so the directory search limit is partitioned by the caller's oid.
app.UseRateLimiter();

app.MapAndesMailerOpenApi();
app.MapAndesMailerHealth();
app.MapUserEndpoints();
app.MapTeamEndpoints();
app.MapTeamMemberEndpoints();
app.MapContactGroupEndpoints();
app.MapEmailEndpoints();
app.MapEmailAttachmentEndpoints();
app.MapEmailTemplateEndpoints();
app.MapEmailTemplateAttachmentEndpoints();

await app.RunAsync();

public partial class Program;
