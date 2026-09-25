namespace Community.Application.DTOs;

public record CommentDto
{
    public Guid Id { get; set; }
    public string? Content { get; set; }
    public Guid? PostId { get; set; }
    public Guid CommentId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public Guid? AuthorId { get; set; }
    public int? LikesCount { get; set; }
    public int? RepliesCount { get; set; }

    // Người xem hiện tại đã thích bình luận này chưa (điền trong handler theo JWT)
    public bool IsLikedByViewer { get; set; }
    public int? Type { get; set; }
    public int? Status { get; set; }
    public int? IsApproved { get; set; }
    public int? IsDeleted { get; set; }
    public int? IsPinned { get; set; }
    public int? IsLiked { get; set; }
    public int? IsCommented { get; set; }
    public int? IsShared { get; set; }
    public int? IsViewed { get; set; }

    // Audit fields from AuditableEntity
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}