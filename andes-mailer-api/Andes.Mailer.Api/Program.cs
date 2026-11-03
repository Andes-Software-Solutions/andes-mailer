using Andes.Mailer.Api.Configuration;
using Andes.Mailer.Api.Endpoints;
using Andes.Mailer.Api.Health;
using Andes.Mailer.Api.Problems;
using Andes.Mailer.Repository.Sql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAndesMailerProblemDetails();
builder.Services.AddAndesMailerOpenApi();
builder.Services.AddAndesMailerValidation();
builder.Services.AddAndesMailerSqlPersistence(builder.Configuration);
builder.Services.AddAndesMailerHealth();
builder.Services.AddAndesMailerAuthentication(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

// Explicit: the auto-added pair would run ahead of UseStatusCodePages and leave 401 and 403 with empty bodies.
app.UseAuthentication();
app.UseAuthorization();

app.MapAndesMailerOpenApi();
app.MapAndesMailerHealth();
app.MapUserEndpoints();

await app.RunAsync();

public partial class Program;
