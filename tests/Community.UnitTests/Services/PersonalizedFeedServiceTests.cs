using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Community.UnitTests.Services;

public class PersonalizedFeedServiceTests
{
    private readonly Mock<IUserFollowRepository> _userFollowRepo = new();
    private readonly Mock<IPostBookmarkRepository> _bookmarkRepo = new();
    private readonly Mock<IPostLikeRepository> _likeRepo = new();
    private readonly Mock<IPostRepository> _postRepo = new();

    private PersonalizedFeedService CreateService()
    {
        return new PersonalizedFeedService(
            _userFollowRepo.Object,
            _bookmarkRepo.Object,
            _likeRepo.Object,
            _postRepo.Object,
            NullLogger<PersonalizedFeedService>.Instance);
    }

    [Fact]
    public void RankPosts_WithRelevantMode_PrioritizesFollowedTechZoneAndMatchingTags()
    {
        // Arrange
        var service = CreateService();
        var followedZoneId = Guid.NewGuid();
        var otherZoneId = Guid.NewGuid();

        var userContext = new UserFeedContext
        {
            UserId = Guid.NewGuid(),
            FollowedTechZoneIds = new HashSet<Guid> { followedZoneId },
            TagAffinities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dotnet"] = 6.0,
                ["csharp"] = 4.0
            }
        };

        var matchedPost = new Post
        {
            Id = Guid.NewGuid(),
            Content = "# Xây dựng Clean Architecture với .NET 10\n```csharp\npublic class DevRadar {}\n```",
            TechZoneId = followedZoneId,
            Tags = "dotnet, csharp",
            LikesCount = 5,
            CommentsCount = 2,
            CreatedDate = DateTime.UtcNow.AddHours(-1)
        };

        var otherPost = new Post
        {
            Id = Guid.NewGuid(),
            Content = "# Giới thiệu Ruby on Rails cơ bản\nNội dung tổng quan về Ruby...",
            TechZoneId = otherZoneId,
            Tags = "ruby, rails",
            LikesCount = 5,
            CommentsCount = 2,
            CreatedDate = DateTime.UtcNow.AddHours(-1)
        };

        var candidates = new List<Post> { otherPost, matchedPost };

        // Act
        var ranked = service.RankPosts(candidates, userContext, "relevant");

        // Assert
        Assert.Equal(2, ranked.Count);
        Assert.Equal(matchedPost.Id, ranked[0].Id);
        Assert.Equal(otherPost.Id, ranked[1].Id);
    }

    [Fact]
    public void RankPosts_WithTrendingMode_PrioritizesHighEngagement()
    {
        // Arrange
        var service = CreateService();
        var userContext = new UserFeedContext();

        var viralPost = new Post
        {
            Id = Guid.NewGuid(),
            Content = "# Công bố DevRadar 2.0\nBản phát hành với nhiều tính năng vượt trội...",
            LikesCount = 100,
            CommentsCount = 45,
            BookmarksCount = 30,
            SharesCount = 15,
            CreatedDate = DateTime.UtcNow.AddHours(-2)
        };

        var normalPost = new Post
        {
            Id = Guid.NewGuid(),
            Content = "# Thảo luận nhỏ về Docker\nCách tối ưu hóa Dockerfile đơn giản",
            LikesCount = 2,
            CommentsCount = 1,
            CreatedDate = DateTime.UtcNow.AddHours(-2)
        };

        var candidates = new List<Post> { normalPost, viralPost };

        // Act
        var ranked = service.RankPosts(candidates, userContext, "trending");

        // Assert
        Assert.Equal(viralPost.Id, ranked[0].Id);
        Assert.Equal(normalPost.Id, ranked[1].Id);
    }

    [Fact]
    public void RankPosts_WithLatestMode_SortsByCreatedDateDescending()
    {
        // Arrange
        var service = CreateService();
        var userContext = new UserFeedContext();

        var olderPost = new Post
        {
            Id = Guid.NewGuid(),
            Content = "# Bài cũ",
            CreatedDate = DateTime.UtcNow.AddHours(-10),
            LikesCount = 1000
        };

        var newerPost = new Post
        {
            Id = Guid.NewGuid(),
            Content = "# Bài mới",
            CreatedDate = DateTime.UtcNow.AddMinutes(-5),
            LikesCount = 0
        };

        var candidates = new List<Post> { olderPost, newerPost };

        // Act
        var ranked = service.RankPosts(candidates, userContext, "latest");

        // Assert
        Assert.Equal(newerPost.Id, ranked[0].Id);
        Assert.Equal(olderPost.Id, ranked[1].Id);
    }

    [Fact]
    public void RankPosts_InteractedPost_ReceivesSeenPenalty()
    {
        // Arrange
        var service = CreateService();
        var seenPostId = Guid.NewGuid();
        var unreadPostId = Guid.NewGuid();

        var userContext = new UserFeedContext
        {
            UserId = Guid.NewGuid(),
            InteractedPostIds = new HashSet<Guid> { seenPostId }
        };

        var seenPost = new Post
        {
            Id = seenPostId,
            Content = "# Bài đã tương tác",
            LikesCount = 5,
            CreatedDate = DateTime.UtcNow.AddHours(-2)
        };

        var unreadPost = new Post
        {
            Id = unreadPostId,
            Content = "# Bài chưa đọc",
            LikesCount = 5,
            CreatedDate = DateTime.UtcNow.AddHours(-2)
        };

        var candidates = new List<Post> { seenPost, unreadPost };

        // Act
        var ranked = service.RankPosts(candidates, userContext, "relevant");

        // Assert — Bài chưa đọc phải được ưu tiên đứng trước bài đã đọc/tương tác
        Assert.Equal(unreadPost.Id, ranked[0].Id);
        Assert.Equal(seenPost.Id, ranked[1].Id);
    }

    [Fact]
    public void RankPosts_ExcludesOwnPosts_WhenUserIdIsPresent()
    {
        // Arrange
        var service = CreateService();
        var myUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var userContext = new UserFeedContext
        {
            UserId = myUserId
        };

        var myPost = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = myUserId,
            Content = "# Bài của chính tôi",
            LikesCount = 100,
            CreatedDate = DateTime.UtcNow
        };

        var otherPost = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = otherUserId,
            Content = "# Bài của tác giả khác",
            LikesCount = 1,
            CreatedDate = DateTime.UtcNow
        };

        var candidates = new List<Post> { myPost, otherPost };

        // Act
        var ranked = service.RankPosts(candidates, userContext, "relevant");

        // Assert — Bài của chính mình phải bị ẩn hoàn toàn khỏi kết quả
        Assert.Single(ranked);
        Assert.Equal(otherPost.Id, ranked[0].Id);
        Assert.DoesNotContain(ranked, p => p.AuthorId == myUserId);
    }
}
