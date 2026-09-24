using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Bình luận (FR-24 → FR-28), hỗ trợ reply lồng nhau qua ParentCommentId.
    /// Trạng thái theo enum ContentStatus: Draft = 0, Published = 1, Hidden = 2, Deleted = 3.
    /// </summary>
    [Table("Comments")]
    public class Comment : AuditableEntity<Guid>
    {
        public string? Content { get; set; }
        public Guid? PostId { get; set; }
        public Guid CommentId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public Guid? AuthorId { get; set; }
        public int? LikesCount { get; set; }
        public int? RepliesCount { get; set; }
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
