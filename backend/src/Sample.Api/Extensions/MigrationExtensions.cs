namespace Sample.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using Sample.Infrastructure.Persistence;

public static class MigrationExtensions
{
    public async static Task<IApplicationBuilder> ApplyMigration(this WebApplication app)
    {
        using var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        var context = serviceScope.ServiceProvider.GetRequiredService<SampleDbContext>();
        await context.Database.MigrateAsync();

        return app;
    }
}