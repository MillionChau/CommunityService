using AutoMapper;
using Community.Application.Features.Posts.Commands.SharePost;
using Community.Application.Features.Posts.Queries.GetMyBookmarks;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.UnitTests.TestFixtures;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Community.UnitTests.Features.Interactions;

/// <summary>
/// Unit test UC-22 (chia sẻ bài — Guest được phép) và UC-20 (danh sách đã lưu của tôi).
/// </summary>
public class ShareAndMyBookmarksHandlerTests
{
    private readonly TestFixture _f = new();

    // ================= SHARE POST (UC-22 / FR-22) =================

    [Fact]
    public async Task Share_ShouldIncrementSharesCount_AndReturnShareLink()
    {
        // Arrange — Guest (chưa đăng nhập) vẫn share được theo ma trận quyền SRS
        var post = TestFixture.CreatePost();
        post.SharesCount = 7;
        _f.Logout();
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new SharePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.Notifier.Object,
            _f.NotificationClient.Object, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new SharePostCommand(post.Id), CancellationToken.None);

        // Assert — 7 → 8, link chuẩn dạng /posts/{id}
        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Data!.SharesCount);
        Assert.Equal($"/posts/{post.Id}", result.Data.ShareLink);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Share_ByLoggedInUser_ShouldNotifyPostAuthor()
    {
        // Arrange — bob share bài của alice → alice nhận PostShared
        var bobId = Guid.NewGuid();
        var aliceId = Guid.NewGuid();
        var post = TestFixture.CreatePost(authorId: aliceId);
        _f.LoginAs(bobId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new SharePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.Notifier.Object,
            _f.NotificationClient.Object, _f.CurrentUser.Object);

        // Act
        await handler.Handle(new SharePostCommand(post.Id), CancellationToken.None);

        // Assert
        _f.NotificationClient.Verify(c => c.SendAsync(
            It.Is<NotificationOutbound>(n =>
                n.RecipientId == aliceId && n.Type == NotificationTypes.PostShared && n.ActorId == bobId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Share_AsGuest_ShouldNotNotify()
    {
        // Arrange — guest share: không biết ai share → không thông báo
        var post = TestFixture.CreatePost();
        _f.Logout();
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new SharePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.Notifier.Object,
            _f.NotificationClient.Object, _f.CurrentUser.Object);

        // Act
        await handler.Handle(new SharePostCommand(post.Id), CancellationToken.None);

        // Assert
        _f.NotificationClient.Verify(c => c.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Share_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.Logout();
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new SharePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.Notifier.Object,
            _f.NotificationClient.Object, _f.CurrentUser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Community.Application.Common.Exceptions.NotFoundException>(() =>
            handler.Handle(new SharePostCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Share_ShouldBroadcastPostShared()
    {
        // Arrange
        var post = TestFixture.CreatePost();
        _f.Logout();
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new SharePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.Notifier.Object,
            _f.NotificationClient.Object, _f.CurrentUser.Object);

        // Act
        await handler.Handle(new SharePostCommand(post.Id), CancellationToken.None);

        // Assert
        _f.Notifier.Verify(n => n.NotifyFeedAsync("post-shared", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _f.Notifier.Verify(n => n.NotifyPostAsync(post.Id, "post-shared", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ================= GET MY BOOKMARKS (UC-20 / FR-20) =================

    private void SetupBookmarkRepo(List<PostBookmark> bookmarks)
    {
        var data = _f.ToAsyncQueryable(bookmarks);
        _f.PostBookmarkRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostBookmark, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostBookmark, bool>>? expr, CancellationToken _) =>
                expr == null ? data : data.Where(expr));
    }

    private void SetupPostRepo(List<Post> posts)
    {
        var data = _f.ToAsyncQueryable(posts);
        _f.PostRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Post, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Post, bool>>? expr, CancellationToken _) =>
                expr == null ? data : data.Where(expr));
    }

    [Fact]
    public async Task GetMyBookmarks_ShouldReturnOnlyMyBookmarkedPosts_WithIsBookmarkedTrue()
    {
        // Arrange — user đã lưu 2 bài
        var userId = Guid.NewGuid();
        var postA = TestFixture.CreatePost();
        var postB = TestFixture.CreatePost();
        var bookmarks = new List<PostBookmark>
        {
            new() { Id = Guid.NewGuid(), PostId = postA.Id, UserId = userId, CreatedDate = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = Guid.NewGuid(), PostId = postB.Id, UserId = userId, CreatedDate = DateTime.UtcNow }
        };

        _f.LoginAs(userId, "bob");
        SetupBookmarkRepo(bookmarks);
        SetupPostRepo(new List<Post> { postA, postB });

        var handler = new GetMyBookmarksQueryHandler(
            _f.PostBookmarkRepository.Object, _f.PostRepository.Object, _f.CurrentUser.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(new GetMyBookmarksQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        // Assert — 2 bài, đều IsBookmarkedByViewer = true, mới lưu lên trước
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.All(result.Data.Items, p => Assert.True(p.IsBookmarkedByViewer));
        Assert.Equal(postB.Id, result.Data.Items.First().Id); // lưu sau → lên trước
    }

    [Fact]
    public async Task GetMyBookmarks_WhenNotAuthenticated_ShouldThrowUnauthorized()
    {
        // Arrange
        _f.Logout();
        var handler = new GetMyBookmarksQueryHandler(
            _f.PostBookmarkRepository.Object, _f.PostRepository.Object, _f.CurrentUser.Object, _f.Mapper);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetMyBookmarksQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetMyBookmarks_ShouldNotIncludeOtherUsersBookmarks()
    {
        // Arrange — repo lọc theo userId trong handler; mock trả toàn bộ nhưng
        // expression Where(b => b.UserId == userId) phải loại bookmark của người khác
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var myPost = TestFixture.CreatePost();
        var bookmarks = new List<PostBookmark>
        {
            new() { Id = Guid.NewGuid(), PostId = myPost.Id, UserId = userId },
            new() { Id = Guid.NewGuid(), PostId = TestFixture.CreatePost().Id, UserId = otherUserId }
        };

        _f.LoginAs(userId, "bob");
        SetupBookmarkRepo(bookmarks);
        SetupPostRepo(new List<Post> { myPost });

        var handler = new GetMyBookmarksQueryHandler(
            _f.PostBookmarkRepository.Object, _f.PostRepository.Object, _f.CurrentUser.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(new GetMyBookmarksQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        // Assert — chỉ bookmark của mình
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal(myPost.Id, result.Data.Items.First().Id);
    }
}
