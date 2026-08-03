namespace Sample.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.Tasks;

/// <summary>
/// Maps <see cref="TaskItem"/> to the <c>tasks</c> table with:
/// <list type="bullet">
///   <item>UUID surrogate PK with default <c>gen_random_uuid()</c>.</item>
///   <item><see cref="TaskTitle"/> as an owned value type (single column <c>Title</code>).</item>
///   <item>Status and Priority as <c>text</c> columns via the enum-to-string conversion for readability.</item>
///   <item>Auditable timestamps via the auditable base entity.</item>
/// </list>
/// </summary>
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Title is a ValueObject; store its single component as a column.
        builder.OwnsOne(t => t.Title, titleBuilder =>
        {
            titleBuilder.Property(t => t.Value)
                .HasColumnName("Title")
                .HasMaxLength(TaskTitle.MaxLength)
                .IsRequired();
        });

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        // Stored as text for human-readable queries; enums still map fine but
        // text is friendlier in psql.
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.OwnerId).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
        builder.Property(t => t.CompletedAt);

        builder.HasIndex(t => t.OwnerId);
        builder.HasIndex(t => new { t.Status, t.Priority });
    }
}
