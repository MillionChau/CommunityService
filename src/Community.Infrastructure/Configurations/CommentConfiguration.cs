using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .HasColumnType("text");

        // Truy vấn bình luận theo bài viết (FR-24) + lọc trạng thái
        builder.HasIndex(c => new { c.PostId, c.Status });

        // Dựng cây reply lồng nhau (FR-25)
        builder.HasIndex(c => c.ParentCommentId);

        // Quan hệ với Post khai báo ở PostConfiguration (Cascade)
        builder.HasOne<Post>()
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
