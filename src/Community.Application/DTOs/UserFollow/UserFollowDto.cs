namespace Community.Application.DTOs;

public record UserFollowDto
{
    public Guid Id { get; set; }
    public Guid FollowerId { get; set; }
    public Guid FolloweeId { get; set; }
    public DateTime CreatedDate { get; set; }
}

public record UserFollowSummaryDto
{
    public Guid UserId { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowing { get; set; }
}
