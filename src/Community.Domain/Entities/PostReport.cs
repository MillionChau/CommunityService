using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Báo cáo vi phạm một bài viết (FR-23 / UC-23).
    /// Một người dùng chỉ được tạo MỘT báo cáo đang chờ duyệt cho mỗi bài viết
    /// (ràng buộc nghiệp vụ, kiểm tra trong CreatePostReportCommandHandler).
    /// </summary>
    [Table("PostReports")]
    public class PostReport : AuditableEntity<Guid>
    {
        public Guid PostId { get; set; }
        public Guid ReporterId { get; set; }

        /// <summary>Lý do báo cáo do người dùng nhập (bắt buộc).</summary>
        public string? Reason { get; set; }

        /// <summary>Pending = 0, Approved = 1, Rejected = 2 (xem ReportStatus).</summary>
        public int? Status { get; set; }

        /// <summary>Ghi chú của Admin khi duyệt/từ chối báo cáo (tùy chọn).</summary>
        public string? ReviewNote { get; set; }
    }
}