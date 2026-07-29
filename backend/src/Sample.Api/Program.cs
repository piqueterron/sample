using Sample.Api.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpoints();
builder.Services.AddDispatcher();
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddSpaCors(builder.Configuration);
builder.Services.AddKeycloakAuth(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

await app.RunAsync();