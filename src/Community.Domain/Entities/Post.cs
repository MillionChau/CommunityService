using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    [Table("Posts")]
    public class Post : AuditableEntity<Guid>
    {
        public string? Content { get; set; }
        public Guid? AuthorId { get; set; }
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
