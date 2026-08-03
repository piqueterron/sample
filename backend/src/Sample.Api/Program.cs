using Sample.Api.Extensions;
using Sample.Infrastructure.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpoints();
builder.Services.AddDispatcher();
builder.Services.AddApplicationServices();
builder.Services.AddSamplePersistence(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddSpaCors(builder.Configuration);
builder.Services.AddKeycloakAuth(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    await app.ApplyMigration();
}

app.UseHttpsRedirection();

await app.RunAsync();

// Expose the implicit `Program` type as public so WebApplicationFactory<Program>
// (used by Sample.Api.IntegrationTests) can reference it for in-memory boot.
public partial class Program;