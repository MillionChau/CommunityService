using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Bài viết cộng đồng (FR-10 → FR-23). Trạng thái theo enum ContentStatus:
    /// Draft = 0, Published = 1, Hidden = 2 (bị ẩn do vi phạm), Deleted = 3 (soft delete).
    /// </summary>
    [Table("Posts")]
    public class Post : AuditableEntity<Guid>
    {
        public string? Content { get; set; }
        public Guid? AuthorId { get; set; }
        public ICollection<Comment>? Comments { get; set; }
        public ICollection<PostReport>? Reports { get; set; }
        public int? LikesCount { get; set; }
        public int? CommentsCount { get; set; }
        public int? SharesCount { get; set; }
        public int? ViewsCount { get; set; }
        public int? Type { get; set; }
        public int? Status { get; set; }
        public int? IsApproved { get; set; }
        public int? IsDeleted { get; set; }
        public int? IsPinned { get; set; }
        public int? IsLiked { get; set; }
        public int? IsCommented { get; set; }
        public int? IsShared { get; set; }
        public int? IsViewed { get; set; }
    }
}
