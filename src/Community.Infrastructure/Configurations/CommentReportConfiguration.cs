using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class CommentReportConfiguration : IEntityTypeConfiguration<CommentReport>
{
    public void Configure(EntityTypeBuilder<CommentReport> builder)
    {
        builder.ToTable("CommentReports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason)
            .HasMaxLength(500);

        builder.Property(r => r.ReviewNote)
            .HasMaxLength(500);

        // Hàng chờ duyệt: lọc theo Status + sort CreatedDate
        builder.HasIndex(r => new { r.Status, r.CreatedDate });

        // Lịch sử báo cáo theo comment
        builder.HasIndex(r => r.CommentId);

        // 1 user chỉ được MỘT báo cáo đang chờ duyệt cho mỗi comment (Status = 0)
        builder.HasIndex(r => new { r.CommentId, r.ReporterId })
            .HasFilter("\"Status\" = 0")
            .IsUnique()
            .HasDatabaseName("UX_CommentReports_OnePendingPerUserComment");

        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
