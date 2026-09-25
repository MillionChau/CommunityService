using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Lượt thích bình luận (FR-28 / UC-28). Cặp (CommentId, UserId) là duy nhất
    /// (unique index ở tầng DB). Toggle: thêm khi thích, xóa khi bỏ thích.
    /// </summary>
    [Table("CommentLikes")]
    public class CommentLike : AuditableEntity<Guid>
    {
        public Guid CommentId { get; set; }
        public Guid UserId { get; set; }
    }
}
