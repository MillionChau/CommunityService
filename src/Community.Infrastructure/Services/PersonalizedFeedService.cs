using System.Text.RegularExpressions;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Community.Infrastructure.Services;

/// <summary>
/// Thực thi thuật toán Dev-Rank: Xếp hạng bảng tin cộng đồng cá nhân hóa theo ngữ cảnh từng lập trình viên.
/// </summary>
public class PersonalizedFeedService : IPersonalizedFeedService
{
    private readonly IUserFollowRepository _userFollowRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IPostLikeRepository _likeRepository;
    private readonly IPostRepository _postRepository;
    private readonly ILogger<PersonalizedFeedService> _logger;

    public PersonalizedFeedService(
        IUserFollowRepository userFollowRepository,
        IPostBookmarkRepository bookmarkRepository,
        IPostLikeRepository likeRepository,
        IPostRepository postRepository,
        ILogger<PersonalizedFeedService> logger)
    {
        _userFollowRepository = userFollowRepository;
        _bookmarkRepository = bookmarkRepository;
        _likeRepository = likeRepository;
        _postRepository = postRepository;
        _logger = logger;
    }

    public async Task<UserFeedContext> BuildUserContextAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var context = new UserFeedContext { UserId = userId };
        if (!context.IsAuthenticated)
        {
            return context;
        }

        try
        {
            var uid = userId!.Value;

            // 1. Tác giả mà người dùng đang follow (Author Affinity)
            var followeeIds = await _userFollowRepository
                .GetByExpression(f => f.FollowerId == uid, cancellationToken)
                .Select(f => f.FolloweeId)
                .ToListAsync(cancellationToken);

            foreach (var fId in followeeIds)
                context.FollowedAuthorIds.Add(fId);

            // 2. Bài viết đã tương tác gần đây (Bookmarks & Likes) để trích xuất Tag & TechZone
            var recentBookmarkPostIds = await _bookmarkRepository
                .GetByExpression(b => b.UserId == uid, cancellationToken)
                .OrderByDescending(b => b.CreatedDate)
                .Take(40)
                .Select(b => b.PostId)
                .ToListAsync(cancellationToken);

            var recentLikePostIds = await _likeRepository
                .GetByExpression(l => l.UserId == uid, cancellationToken)
                .OrderByDescending(l => l.CreatedDate)
                .Take(40)
                .Select(l => l.PostId)
                .ToListAsync(cancellationToken);

            foreach (var id in recentBookmarkPostIds)
                context.InteractedPostIds.Add(id);
            foreach (var id in recentLikePostIds)
                context.InteractedPostIds.Add(id);

            var interactedIds = context.InteractedPostIds.ToList();
            if (interactedIds.Count > 0)
            {
                var interactedPosts = await _postRepository
                    .GetByExpression(p => interactedIds.Contains(p.Id), cancellationToken)
                    .Select(p => new { p.Id, p.TechZoneId, p.Tags })
                    .ToListAsync(cancellationToken);

                foreach (var p in interactedPosts)
                {
                    if (p.TechZoneId.HasValue)
                    {
                        context.FollowedTechZoneIds.Add(p.TechZoneId.Value);
                    }

                    if (!string.IsNullOrWhiteSpace(p.Tags))
                    {
                        var weight = recentBookmarkPostIds.Contains(p.Id) ? 3.0 : 1.5;
                        var tags = SplitTags(p.Tags);
                        foreach (var tag in tags)
                        {
                            if (context.TagAffinities.TryGetValue(tag, out var currentVal))
                                context.TagAffinities[tag] = currentVal + weight;
                            else
                                context.TagAffinities[tag] = weight;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xây dựng UserFeedContext cho user {UserId}. Sử dụng context mặc định.", userId);
        }

        return context;
    }

    public List<Post> RankPosts(
        IEnumerable<Post> candidatePosts,
        UserFeedContext userContext,
        string? sortBy,
        IReadOnlyDictionary<Guid, int>? authorReputations = null)
    {
        // Ẩn bài viết của bản thân khỏi Newsfeed xếp hạng cá nhân hóa
        var postsToRank = candidatePosts;
        if (userContext.UserId.HasValue)
        {
            postsToRank = postsToRank.Where(p => p.AuthorId != userContext.UserId.Value);
        }

        var mode = sortBy?.Trim().ToLowerInvariant();
        if (mode == "latest")
        {
            return postsToRank.OrderByDescending(p => p.CreatedDate).ToList();
        }

        var now = DateTime.UtcNow;
        var scoredList = new List<(Post Post, double Score)>();

        foreach (var post in postsToRank)
        {
            var hoursAgo = Math.Max(0.0, (now - post.CreatedDate).TotalHours);

            // ===== 1. S_engagement: Sức hút tương tác =====
            var nLike = Math.Max(0, post.LikesCount ?? 0);
            var nComment = Math.Max(0, post.CommentsCount ?? 0);
            var nBookmark = Math.Max(0, post.BookmarksCount ?? 0);
            var nShare = Math.Max(0, post.SharesCount ?? 0);

            var rawEngagement = (1.0 * nLike) + (3.0 * nComment) + (4.5 * nBookmark) + (5.0 * nShare);

            // Tín dụng bài mới (Exploration boost trong 6h đầu)
            if (hoursAgo <= 6.0)
            {
                rawEngagement += 5.0;
            }

            var sEngagement = Math.Log10(1.0 + rawEngagement);

            // ===== 2. S_quality: Chất lượng & chiều sâu kỹ thuật =====
            var aiScore = 0.85; // Mặc định bài Published đã pass AI moderation

            var authorPrp = post.AuthorId.HasValue && authorReputations != null
                ? authorReputations.GetValueOrDefault(post.AuthorId.Value, 0)
                : 0;

            var authorTierScore = authorPrp >= 1500 ? 1.0
                : authorPrp >= 600 ? 0.8
                : authorPrp >= 200 ? 0.6
                : 0.4;

            var hasCode = !string.IsNullOrEmpty(post.Content) && post.Content.Contains("```");
            var isLongForm = !string.IsNullOrEmpty(post.Content) && post.Content.Length > 300;
            var depthBonus = (hasCode ? 0.20 : 0.0) + (isLongForm ? 0.05 : 0.0);

            var sQuality = (0.40 * aiScore) + (0.35 * authorTierScore) + (0.25 * Math.Min(1.0, depthBonus + 0.3));

            if (mode == "trending")
            {
                // Trending Feed: Tương tác mạnh + suy giảm thời gian nhanh + chất lượng
                var trendingDecay = 1.0 / Math.Pow(1.0 + 0.08 * hoursAgo, 1.5);
                var trendingScore = sEngagement * trendingDecay * (0.6 + 0.4 * sQuality);
                scoredList.Add((post, trendingScore));
                continue;
            }

            // ===== 3. S_affinity: Độ tương thích cá nhân (Relevant Feed) =====
            double sAffinity;
            if (!userContext.IsAuthenticated)
            {
                sAffinity = 0.3; // Mức trung tính cho guest
            }
            else
            {
                var zoneMatch = post.TechZoneId.HasValue && userContext.FollowedTechZoneIds.Contains(post.TechZoneId.Value) ? 1.0 : 0.1;

                var postTags = SplitTags(post.Tags);
                double tagSim = 0.0;
                if (postTags.Length > 0 && userContext.TagAffinities.Count > 0)
                {
                    var sumAffinity = postTags.Sum(t => userContext.TagAffinities.GetValueOrDefault(t, 0.0));
                    if (sumAffinity > 0)
                    {
                        tagSim = Math.Min(1.0, sumAffinity / (Math.Sqrt(postTags.Length + 1) * 2.0));
                    }
                }

                var authorAffinity = post.AuthorId.HasValue && userContext.FollowedAuthorIds.Contains(post.AuthorId.Value) ? 1.0 : 0.0;

                sAffinity = (0.40 * zoneMatch) + (0.35 * tagSim) + (0.25 * authorAffinity);
            }

            // ===== 4. Decay: Suy giảm trọng lực theo thời gian =====
            var decay = 1.0 / Math.Pow(1.0 + 0.05 * hoursAgo, 1.2);

            // ===== 5. Penalty: Phạt bài đã tương tác (Seen/Interacted Penalty) =====
            var penalty = 1.0;
            if (userContext.InteractedPostIds.Contains(post.Id))
            {
                penalty = 0.35; // Giảm ưu tiên bài đã like/bookmark để hiện bài mới
            }

            // ===== Điểm tổng hợp Dev-Rank =====
            var baseScore = (0.45 * sAffinity) + (0.30 * sQuality) + (0.25 * sEngagement);
            var finalScore = baseScore * decay * penalty;

            scoredList.Add((post, finalScore));
        }

        return scoredList
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Post.CreatedDate)
            .Select(x => x.Post)
            .ToList();
    }

    private static string[] SplitTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return Array.Empty<string>();
        return tags.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(t => t.Trim().ToLowerInvariant())
                   .Where(t => t.Length > 0)
                   .Distinct()
                   .ToArray();
    }
}
