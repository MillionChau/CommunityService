namespace Community.Application.DTOs;

public class PostDto
{
    public Guid Id { get; set; }
    public string? Content { get; set; }
    public Guid? AuthorId { get; set; }
    public int? LikesCount { get; set; }
    public int? CommentsCount { get; set; }
    public int? SharesCount { get; set; }
    public int? BookmarksCount { get; set; }
    public int? ViewsCount { get; set; }
    public int? Status { get; set; }
    public int? IsApproved { get; set; }
    public Guid? TechZoneId { get; set; }
    public string? TechZoneName { get; set; }
    public string? TechZoneSlug { get; set; }
    public string? Tags { get; set; }
    public Guid? BookmarkFolderId { get; set; }

    // Trạng thái tương tác của NGƯỜI XEM HIỆN TẠI (điền trong handler theo JWT, không map từ entity)
    public bool IsLikedByViewer { get; set; }
    public bool IsBookmarkedByViewer { get; set; }

    // Audit fields from AuditableEntity
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
