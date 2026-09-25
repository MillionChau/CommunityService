using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("PostLikes");

        builder.HasKey(l => l.Id);

        // Ràng buộc dữ liệu: 1 user chỉ được like 1 bài viết MỘT lần (FR-19, chống race condition)
        builder.HasIndex(l => new { l.PostId, l.UserId })
            .IsUnique()
            .HasDatabaseName("UX_PostLikes_PostUser");

        // Truy vấn "các bài user này đã thích"
        builder.HasIndex(l => l.UserId);

        // Like gắn với Post khai báo ở PostConfiguration (Restrict)
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(l => l.PostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
