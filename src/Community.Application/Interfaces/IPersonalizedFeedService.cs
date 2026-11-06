using Community.Domain.Entities;

namespace Community.Application.Interfaces;

/// <summary>
/// Ngữ cảnh hồ sơ sở thích kỹ thuật của người dùng dùng để xếp hạng Feed.
/// </summary>
public class UserFeedContext
{
    public Guid? UserId { get; set; }
    public HashSet<Guid> FollowedAuthorIds { get; set; } = new();
    public HashSet<Guid> FollowedTechZoneIds { get; set; } = new();
    public Dictionary<string, double> TagAffinities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<Guid> InteractedPostIds { get; set; } = new();
    public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
}

/// <summary>
/// Dịch vụ tính toán độ ưu tiên và xếp hạng bài viết bảng tin cá nhân hóa (Thuật toán Dev-Rank).
/// </summary>
public interface IPersonalizedFeedService
{
    /// <summary>
    /// Xây dựng ngữ cảnh tương tác và sở thích của người dùng (từ Following, Bookmarks, Likes).
    /// </summary>
    Task<UserFeedContext> BuildUserContextAsync(Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chấm điểm và sắp xếp danh sách bài viết ứng viên theo độ ưu tiên phù hợp (relevant, trending, hoặc latest).
    /// </summary>
    List<Post> RankPosts(
        IEnumerable<Post> candidatePosts,
        UserFeedContext userContext,
        string? sortBy,
        IReadOnlyDictionary<Guid, int>? authorReputations = null);
}
