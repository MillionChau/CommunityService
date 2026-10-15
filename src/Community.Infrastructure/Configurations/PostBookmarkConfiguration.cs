using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class PostBookmarkConfiguration : IEntityTypeConfiguration<PostBookmark>
{
    public void Configure(EntityTypeBuilder<PostBookmark> builder)
    {
        builder.ToTable("PostBookmarks");

        builder.HasKey(b => b.Id);

        // Ràng buộc dữ liệu: 1 user chỉ lưu 1 bài viết MỘT lần (FR-20)
        builder.HasIndex(b => new { b.PostId, b.UserId })
            .IsUnique()
            .HasDatabaseName("UX_PostBookmarks_PostUser");

        // Truy vấn chính: "danh sách đã lưu của user" (GetMyBookmarks)
        builder.HasIndex(b => new { b.UserId, b.CreatedDate });
        builder.HasIndex(b => new { b.UserId, b.FolderId });

        // Bookmark gắn với Post khai báo ở PostConfiguration (Restrict)
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(b => b.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Folder)
            .WithMany(f => f.Bookmarks)
            .HasForeignKey(b => b.FolderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
