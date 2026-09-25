using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Lưu bài viết (FR-20 / UC-20). Cặp (PostId, UserId) là duy nhất
    /// (unique index ở tầng DB). Toggle: thêm khi lưu, xóa khi bỏ lưu.
    /// Lưu ý: FR-21 (bộ sưu tập/folder) nằm ngoài phạm vi hiện tại.
    /// </summary>
    [Table("PostBookmarks")]
    public class PostBookmark : AuditableEntity<Guid>
    {
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
    }
}
