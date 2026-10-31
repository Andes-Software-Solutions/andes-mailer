using Andes.Mailer.Api.Configuration;
using Andes.Mailer.Api.Health;
using Andes.Mailer.Api.Problems;
using Andes.Mailer.Repository.Sql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAndesMailerProblemDetails();
builder.Services.AddAndesMailerOpenApi();
builder.Services.AddAndesMailerValidation();
builder.Services.AddAndesMailerSqlPersistence(builder.Configuration);
builder.Services.AddAndesMailerHealth();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.MapAndesMailerOpenApi();
app.MapAndesMailerHealth();

await app.RunAsync();

public partial class Program;
