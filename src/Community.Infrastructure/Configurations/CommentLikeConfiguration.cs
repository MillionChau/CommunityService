using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> builder)
    {
        builder.ToTable("CommentLikes");

        builder.HasKey(l => l.Id);

        // Ràng buộc dữ liệu: 1 user chỉ được like 1 bình luận MỘT lần (FR-28)
        builder.HasIndex(l => new { l.CommentId, l.UserId })
            .IsUnique()
            .HasDatabaseName("UX_CommentLikes_CommentUser");

        builder.HasIndex(l => l.UserId);

        // Like gắn với Comment khai báo ở CommentConfiguration (Restrict)
        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(l => l.CommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
