namespace Community.Application.DTOs;

public class TechZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? ColorCode { get; set; }
    public string? Tags { get; set; }
    public int DisplayOrder { get; set; }
    public int Status { get; set; }
    public int PostsCount { get; set; }
    public int FollowersCount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
