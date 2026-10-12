using System.Linq.Expressions;
using Community.Application.Features.Posts.Queries.GetDrafts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.Posts;

/// <summary>
/// Unit tests cho UC-18 (Lấy danh sách bài viết bản nháp):
/// Yêu cầu đăng nhập, chỉ trả về bài nháp của chính người dùng, phân trang đúng.
/// </summary>
public class GetDraftPostsQueryHandlerTests
{
    private readonly TestFixture _f = new();

    [Fact]
    public async Task Handle_WhenUnauthenticated_ShouldThrowUnauthorized()
    {
        // Arrange
        _f.Logout();
        var handler = new GetDraftPostsQueryHandler(_f.PostRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetDraftPostsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnDraftsOfCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _f.LoginAs(userId, "alice");

        var userDraft1 = TestFixture.CreatePost(authorId: userId, status: (int)ContentStatus.Draft, content: "Draft 1");
        var userDraft2 = TestFixture.CreatePost(authorId: userId, status: (int)ContentStatus.Draft, content: "Draft 2");
        var otherDraft = TestFixture.CreatePost(authorId: otherUserId, status: (int)ContentStatus.Draft, content: "Other Draft");
        var userPublished = TestFixture.CreatePost(authorId: userId, status: (int)ContentStatus.Published, content: "Published Post");

        var allPosts = _f.ToAsyncQueryable(new List<Post>
        {
            userDraft1,
            userDraft2,
            otherDraft,
            userPublished
        });

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? allPosts : allPosts.Where(expr));

        var handler = new GetDraftPostsQueryHandler(_f.PostRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetDraftPostsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
        Assert.All(result.Data.Items, item =>
        {
            Assert.Equal(userId, item.AuthorId);
            Assert.Equal((int)ContentStatus.Draft, item.Status);
        });
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _f.LoginAs(userId, "alice");

        var drafts = Enumerable.Range(1, 15)
            .Select(i => TestFixture.CreatePost(authorId: userId, status: (int)ContentStatus.Draft, content: $"Draft {i}"))
            .ToList();

        var allPosts = _f.ToAsyncQueryable(drafts);

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? allPosts : allPosts.Where(expr));

        var handler = new GetDraftPostsQueryHandler(_f.PostRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act - Lấy trang 2 với pageSize = 10 -> còn 5 bản ghi
        var result = await handler.Handle(new GetDraftPostsQuery { Page = 2, PageSize = 10 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(15, result.Data!.TotalCount);
        Assert.Equal(5, result.Data.Items.Count());
        Assert.Equal(2, result.Data.TotalPages);
    }
}
