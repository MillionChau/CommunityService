using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class TechZoneConfiguration : IEntityTypeConfiguration<TechZone>
{
    public void Configure(EntityTypeBuilder<TechZone> builder)
    {
        builder.ToTable("TechZones");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("UX_TechZones_Slug");

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.IconUrl)
            .HasMaxLength(255);

        builder.Property(t => t.ColorCode)
            .HasMaxLength(20);

        builder.Property(t => t.Tags)
            .HasMaxLength(500);

        builder.HasIndex(t => new { t.Status, t.DisplayOrder });

        builder.HasMany(t => t.Posts)
            .WithOne(p => p.TechZone)
            .HasForeignKey(p => p.TechZoneId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
