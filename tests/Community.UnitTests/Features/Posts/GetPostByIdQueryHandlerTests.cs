using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Features.Posts.Queries.GetPostById;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Community.UnitTests.Features.Posts;

/// <summary>
/// Unit test UC-11 (xem chi tiết bài viết): 404 khi không tồn tại,
/// điền IsLikedByViewer/IsBookmarkedByViewer theo JWT, guest không có flags.
/// </summary>
public class GetPostByIdQueryHandlerTests
{
    private readonly TestFixture _f = new();

    private void SetupLikeRepo(Mock<IPostLikeRepository> mock, List<PostLike> items)
    {
        var data = _f.ToAsyncQueryable(items);
        mock.Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostLike, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostLike, bool>>? expr, CancellationToken _) =>
                expr == null ? data : data.Where(expr));
    }

    private void SetupBookmarkRepo(Mock<IPostBookmarkRepository> mock, List<PostBookmark> items)
    {
        var data = _f.ToAsyncQueryable(items);
        mock.Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostBookmark, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostBookmark, bool>>? expr, CancellationToken _) =>
                expr == null ? data : data.Where(expr));
    }

    private GetPostByIdQueryHandler BuildHandler()
        => new(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.PostBookmarkRepository.Object,
            _f.Mapper, _f.CurrentUser.Object);

    [Fact]
    public async Task Handle_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.Logout();
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildHandler().Handle(new GetPostByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AsGuest_ShouldReturnPostWithoutViewerFlags()
    {
        // Arrange
        var post = TestFixture.CreatePost(status: (int)ContentStatus.Published);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.Logout();

        // Act
        var result = await BuildHandler().Handle(new GetPostByIdQuery(post.Id), CancellationToken.None);

        // Assert — guest: không like/bookmark flags, nhưng data bài đầy đủ
        Assert.True(result.IsSuccess);
        Assert.Equal(post.Id, result.Data!.Id);
        Assert.Equal(post.Content, result.Data.Content);
        Assert.False(result.Data.IsLikedByViewer);
        Assert.False(result.Data.IsBookmarkedByViewer);
    }

    [Fact]
    public async Task Handle_AsLoggedInViewer_ShouldFillViewerFlags()
    {
        // Arrange — viewer đã like bài này nhưng chưa bookmark
        var viewerId = Guid.NewGuid();
        var post = TestFixture.CreatePost(status: (int)ContentStatus.Published);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        SetupLikeRepo(_f.PostLikeRepository, new List<PostLike>
        {
            new() { Id = Guid.NewGuid(), PostId = post.Id, UserId = viewerId }
        });
        SetupBookmarkRepo(_f.PostBookmarkRepository, new List<PostBookmark>());

        _f.LoginAs(viewerId, "viewer");

        // Act
        var result = await BuildHandler().Handle(new GetPostByIdQuery(post.Id), CancellationToken.None);

        // Assert
        Assert.True(result.Data!.IsLikedByViewer);
        Assert.False(result.Data.IsBookmarkedByViewer);
    }

    [Fact]
    public async Task Handle_ShouldMapAllCounters()
    {
        // Arrange — counters phải map đầy đủ vào DTO
        var post = TestFixture.CreatePost(status: (int)ContentStatus.Published);
        post.LikesCount = 12;
        post.CommentsCount = 34;
        post.SharesCount = 56;
        post.BookmarksCount = 78;
        post.ViewsCount = 99;
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.Logout();

        // Act
        var result = await BuildHandler().Handle(new GetPostByIdQuery(post.Id), CancellationToken.None);

        // Assert
        Assert.Equal(12, result.Data!.LikesCount);
        Assert.Equal(34, result.Data.CommentsCount);
        Assert.Equal(56, result.Data.SharesCount);
        Assert.Equal(78, result.Data.BookmarksCount);
        Assert.Equal(99, result.Data.ViewsCount);
    }
}
