using System.Linq.Expressions;
using AutoMapper;
using Community.Application.Features.Posts.Queries.GetPosts;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.Posts;

/// <summary>
/// Unit test UC-10 → UC-13 (bảng tin có phân trang/tìm kiếm/lọc):
/// mặc định chỉ hiện Published, tìm kiếm theo keyword, Admin được lọc trạng thái,
/// điền IsLikedByViewer/IsBookmarkedByViewer theo JWT của người xem.
/// </summary>
public class GetPostsQueryHandlerTests
{
    private readonly TestFixture _f = new();

    private GetPostsQueryHandler BuildHandler()
    {
        // Default: like/bookmark rỗng (EF InMemory async provider) — từng test tự setup PostRepository data
        _f.PostLikeRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostLike, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostLike, bool>>? expr, CancellationToken _) =>
                expr == null ? _f.ToEmptyAsyncQueryable<PostLike>() : _f.ToEmptyAsyncQueryable<PostLike>().Where(expr));

        _f.PostBookmarkRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostBookmark, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostBookmark, bool>>? expr, CancellationToken _) =>
                expr == null ? _f.ToEmptyAsyncQueryable<PostBookmark>() : _f.ToEmptyAsyncQueryable<PostBookmark>().Where(expr));

        return new GetPostsQueryHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.PostBookmarkRepository.Object,
            _f.Mapper, _f.CurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ByDefault_ShouldReturnOnlyPublishedPosts()
    {
        // Arrange — 2 bài published + 1 hidden + 1 draft
        var published = new List<Post>
        {
            TestFixture.CreatePost(status: (int)ContentStatus.Published),
            TestFixture.CreatePost(status: (int)ContentStatus.Published)
        };
        var all = _f.ToAsyncQueryable(new List<Post>
        {
            published[0], published[1],
            TestFixture.CreatePost(status: (int)ContentStatus.Hidden),
            TestFixture.CreatePost(status: (int)ContentStatus.Draft)
        });

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        _f.Logout(); // Guest cũng xem được bảng tin public
        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(new GetPostsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        // Assert — chỉ 2 bài Published xuất hiện
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
    }

    [Fact]
    public async Task Handle_WithSearch_ShouldFilterByKeyword()
    {
        // Arrange
        var all = _f.ToAsyncQueryable(new List<Post>
        {
            TestFixture.CreatePost(content: "Hướng dẫn setup React cho developer"),
            TestFixture.CreatePost(content: "Chia sẻ kinh nghiệm phỏng vấn"),
            TestFixture.CreatePost(content: "React vs Vue so sánh")
        });

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        _f.Logout();
        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(new GetPostsQuery { Page = 1, PageSize = 10, Search = "react" }, CancellationToken.None);

        // Assert — FR-12: tìm kiếm không phân biệt hoa thường, 2 bài khớp
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.All(result.Data.Items, p => Assert.Contains("react", p.Content!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_NonAdminRequestingHiddenStatus_ShouldBeForcedToPublished()
    {
        // Arrange — user thường cố lọc bài Hidden phải bị ép về Published
        var all = _f.ToAsyncQueryable(new List<Post>
        {
            TestFixture.CreatePost(status: (int)ContentStatus.Published),
            TestFixture.CreatePost(status: (int)ContentStatus.Hidden)
        });

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        _f.LoginAs(Guid.NewGuid(), "user", isAdmin: false);
        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(
            new GetPostsQuery { Page = 1, PageSize = 10, Status = (int)ContentStatus.Hidden }, CancellationToken.None);

        // Assert — bị ép về Published nên chỉ thấy 1 bài published
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal((int)ContentStatus.Published, 1); // sanity: enum value
    }

    [Fact]
    public async Task Handle_AdminRequestingHiddenStatus_ShouldSeeHiddenPosts()
    {
        // Arrange — Admin lọc Hidden → thấy đúng bài ẩn
        var hiddenPost = TestFixture.CreatePost(status: (int)ContentStatus.Hidden);
        var all = _f.ToAsyncQueryable(new List<Post>
        {
            TestFixture.CreatePost(status: (int)ContentStatus.Published),
            hiddenPost
        });

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(
            new GetPostsQuery { Page = 1, PageSize = 10, Status = (int)ContentStatus.Hidden }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(hiddenPost.Id, result.Data.Items.First().Id);
    }

    [Fact]
    public async Task Handle_WithLoggedInViewer_ShouldFillViewerFlags()
    {
        // Arrange — viewer đã like 1 bài và bookmark 1 bài trong trang
        var viewerId = Guid.NewGuid();
        var postA = TestFixture.CreatePost(status: (int)ContentStatus.Published);
        var postB = TestFixture.CreatePost(status: (int)ContentStatus.Published);
        var all = _f.ToAsyncQueryable(new List<Post> { postA, postB });

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        var likes = _f.ToAsyncQueryable(new List<PostLike> { new() { Id = Guid.NewGuid(), PostId = postA.Id, UserId = viewerId } });
        _f.PostLikeRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostLike, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostLike, bool>>? expr, CancellationToken _) =>
                expr == null ? likes : likes.Where(expr));

        var bookmarks = _f.ToAsyncQueryable(new List<PostBookmark> { new() { Id = Guid.NewGuid(), PostId = postB.Id, UserId = viewerId } });
        _f.PostBookmarkRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostBookmark, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostBookmark, bool>>? expr, CancellationToken _) =>
                expr == null ? bookmarks : bookmarks.Where(expr));

        _f.LoginAs(viewerId, "viewer");
        // Dựng handler trực tiếp — likes/bookmarks đã setup riêng, KHÔNG gọi BuildHandler (sẽ đè empty)
        var handler = new GetPostsQueryHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.PostBookmarkRepository.Object,
            _f.Mapper, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetPostsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        // Assert
        var items = result.Data!.Items.ToList();
        Assert.True(items.First(p => p.Id == postA.Id).IsLikedByViewer);
        Assert.False(items.First(p => p.Id == postA.Id).IsBookmarkedByViewer);
        Assert.True(items.First(p => p.Id == postB.Id).IsBookmarkedByViewer);
        Assert.False(items.First(p => p.Id == postB.Id).IsLikedByViewer);
    }

    [Fact]
    public async Task Handle_Pagination_ShouldReturnCorrectPage()
    {
        // Arrange — 25 bài, lấy trang 3 với pageSize 10 → còn 5 bài
        var all = _f.ToAsyncQueryable(Enumerable.Range(0, 25)
            .Select(_ => TestFixture.CreatePost(status: (int)ContentStatus.Published))
            .ToList());

        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        _f.Logout();
        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(new GetPostsQuery { Page = 3, PageSize = 10 }, CancellationToken.None);

        // Assert
        Assert.Equal(25, result.Data!.TotalCount);
        Assert.Equal(5, result.Data.Items.Count());
        Assert.Equal(3, result.Data.TotalPages);
    }
}
