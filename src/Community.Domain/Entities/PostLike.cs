using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Lượt thích bài viết (FR-19 / UC-19). Cặp (PostId, UserId) là duy nhất
    /// (unique index ở tầng DB). Toggle: thêm khi thích, xóa khi bỏ thích.
    /// </summary>
    [Table("PostLikes")]
    public class PostLike : AuditableEntity<Guid>
    {
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
    }
}
