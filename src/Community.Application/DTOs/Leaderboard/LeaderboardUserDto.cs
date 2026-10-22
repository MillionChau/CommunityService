namespace Community.Application.DTOs.Leaderboard;

public class LeaderboardUserDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int Score { get; set; }
    public int PostCount { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int Rank { get; set; }
    public int TierLevel { get; set; } = 1;
    public string TierTitle { get; set; } = "Code Initiate";
    public string TierBadge { get; set; } = "🌱 Tân thủ";
    public List<string> Badges { get; set; } = new();
}
