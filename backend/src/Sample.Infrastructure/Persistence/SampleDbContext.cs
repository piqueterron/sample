namespace Sample.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Sample.Domain.Tasks;
using Sample.Domain.Users;

/// <summary>
/// The application's write model DbContext. Owns the <c>users</c> aggregate
/// and any additional ones added over time. The connection string and
/// provider (Npgsql) are configured by
/// <see cref="InfrastructureExtensions.AddSamplePersistence"/> in the API
/// composition root.
/// </summary>
public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Applies every IEntityTypeConfiguration<T> in this assembly. New
        // entity configuration files require no manual registration.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SampleDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
