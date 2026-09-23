using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Content)
            .HasColumnType("text");

        // Chỉ mục phục vụ feed: lọc theo Status + sort theo thời gian (FR-10)
        builder.HasIndex(p => new { p.Status, p.CreatedDate });

        // Quan hệ 1-nhiều: Post → Comments. Khi Post bị xóa vật lý, comment bị xóa theo.
        builder.HasMany(p => p.Comments)
            .WithOne()
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Báo cáo của Post: Restrict để không mất lịch sử kiểm duyệt
        builder.HasMany(p => p.Reports)
            .WithOne()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
