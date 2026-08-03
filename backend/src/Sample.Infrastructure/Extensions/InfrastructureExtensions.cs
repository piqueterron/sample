namespace Sample.Infrastructure.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sample.Infrastructure.Persistence;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registers the persistence layer: the <see cref="SampleDbContext"/>
    /// (scoped, matching the Mediator's scoped lifetime) backed by Npgsql,
    /// plus any repository implementations added later. The connection string
    /// is read from <c>ConnectionStrings:SampleDb</c>.
    /// </summary>
    public static IServiceCollection AddSamplePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SampleDb")
            ?? throw new InvalidOperationException("""
                ConnectionStrings:SampleDb is not configured.
                Set it in appsettings.Development.json (host: 'localhost') or
                via the ConnectionStrings__SampleDb env var (Docker: 'postgres')
                """);

        services.AddDbContextPool<SampleDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
