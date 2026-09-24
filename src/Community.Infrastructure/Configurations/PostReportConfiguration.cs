using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class PostReportConfiguration : IEntityTypeConfiguration<PostReport>
{
    public void Configure(EntityTypeBuilder<PostReport> builder)
    {
        builder.ToTable("PostReports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason)
            .HasMaxLength(500);

        builder.Property(r => r.ReviewNote)
            .HasMaxLength(500);

        // Hàng chờ duyệt: lọc theo Status + sort CreatedDate (GetPendingReportsQuery)
        builder.HasIndex(r => new { r.Status, r.CreatedDate });

        // Lịch sử báo cáo theo bài viết (màn hình Admin)
        builder.HasIndex(r => r.PostId);

        // Ràng buộc dữ liệu: 1 user chỉ được MỘT báo cáo đang chờ duyệt cho mỗi bài viết.
        // Unique index bộ phận (partial) chỉ áp dụng cho Status = 0 (Pending) — đặc trưng PostgreSQL.
        builder.HasIndex(r => new { r.PostId, r.ReporterId })
            .HasFilter("\"Status\" = 0")
            .IsUnique()
            .HasDatabaseName("UX_PostReports_OnePendingPerUserPost");

        // Báo cáo gắn với Post khai báo ở PostConfiguration (Restrict)
        builder.HasOne<Post>()
            .WithMany(p => p.Reports)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
