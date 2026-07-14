namespace Community.Application.DTOs;

public class PostDto
{
    public Guid Id { get; set; }
    public string? Content { get; set; }
    public Guid? AuthorId { get; set; }
    public int? LikesCount { get; set; }
    public int? CommentsCount { get; set; }
    public int? SharesCount { get; set; }
    public int? ViewsCount { get; set; }
    
    // Audit fields from AuditableEntity
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
