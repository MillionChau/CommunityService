using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class BookmarkFolderConfiguration : IEntityTypeConfiguration<BookmarkFolder>
{
    public void Configure(EntityTypeBuilder<BookmarkFolder> builder)
    {
        builder.ToTable("BookmarkFolders");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Description)
            .HasMaxLength(500);

        builder.Property(f => f.ColorCode)
            .HasMaxLength(20);

        builder.HasIndex(f => new { f.UserId, f.CreatedDate });

        builder.HasMany(f => f.Bookmarks)
            .WithOne(b => b.Folder)
            .HasForeignKey(b => b.FolderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
