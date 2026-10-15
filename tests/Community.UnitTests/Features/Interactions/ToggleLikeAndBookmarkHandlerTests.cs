using Community.Application.Features.Comments.Commands.ToggleCommentLike;
using Community.Application.Features.Posts.Commands.TogglePostBookmark;
using Community.Application.Features.Posts.Commands.TogglePostLike;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.Interactions;

/// <summary>
/// Unit test UC-19 (like bài), UC-20 (bookmark), UC-28 (like bình luận):
/// toggle 2 chiều đúng counter, không like trùng, thông báo cho tác giả khi được like.
/// </summary>
public class ToggleLikeAndBookmarkHandlerTests
{
    private readonly TestFixture _f = new();

    // ================= TOGGLE POST LIKE (UC-19 / FR-19) =================

    [Fact]
    public async Task TogglePostLike_WhenNotLikedYet_ShouldAddLike_AndIncrementCounter_AndNotifyAuthor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var post = TestFixture.CreatePost(authorId: postAuthorId);
        post.LikesCount = 10;

        _f.LoginAs(userId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.PostLikeRepository.Setup(r => r.GetByPostAndUserAsync(post.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostLike?)null);

        var handler = new TogglePostLikeCommandHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(new TogglePostLikeCommand(post.Id), CancellationToken.None);

        // Assert — thêm like, 10 → 11, tác giả nhận thông báo PostLiked
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsLiked);
        Assert.Equal(11, result.Data.LikesCount);
        _f.PostLikeRepository.Verify(r => r.AddAsync(It.Is<PostLike>(l =>
            l.PostId == post.Id && l.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);

        _f.NotificationClient.Verify(c => c.SendAsync(
            It.Is<NotificationOutbound>(n =>
                n.RecipientId == postAuthorId &&
                n.Type == NotificationTypes.PostLiked &&
                n.ActorId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TogglePostLike_WhenAlreadyLiked_ShouldRemoveLike_AndDecrementCounter_NoNotification()
    {
        // Arrange — user đã like trước đó → lần này là unlike
        var userId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        post.LikesCount = 5;
        var existingLike = new PostLike { Id = Guid.NewGuid(), PostId = post.Id, UserId = userId };

        _f.LoginAs(userId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.PostLikeRepository.Setup(r => r.GetByPostAndUserAsync(post.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLike);

        var handler = new TogglePostLikeCommandHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(new TogglePostLikeCommand(post.Id), CancellationToken.None);

        // Assert — bỏ like, 5 → 4, KHÔNG spam thông báo cho tác giả
        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsLiked);
        Assert.Equal(4, result.Data.LikesCount);
        _f.PostLikeRepository.Verify(r => r.DeleteAsync(existingLike.Id, It.IsAny<CancellationToken>()), Times.Once);
        _f.NotificationClient.Verify(c => c.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TogglePostLike_SelfLike_ShouldNotNotifySelf()
    {
        // Arrange — tác giả like bài của chính mình
        var authorId = Guid.NewGuid();
        var post = TestFixture.CreatePost(authorId: authorId);
        _f.LoginAs(authorId, "alice");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.PostLikeRepository.Setup(r => r.GetByPostAndUserAsync(post.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostLike?)null);

        var handler = new TogglePostLikeCommandHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act
        await handler.Handle(new TogglePostLikeCommand(post.Id), CancellationToken.None);

        // Assert
        _f.NotificationClient.Verify(c => c.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TogglePostLike_WhenNotAuthenticated_ShouldThrowUnauthorized()
    {
        // Arrange
        _f.Logout();
        var handler = new TogglePostLikeCommandHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new TogglePostLikeCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task TogglePostLike_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new TogglePostLikeCommandHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Community.Application.Common.Exceptions.NotFoundException>(() =>
            handler.Handle(new TogglePostLikeCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task TogglePostLike_ShouldBroadcastLikeChangedToFeedAndPost()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        _f.LoginAs(userId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.PostLikeRepository.Setup(r => r.GetByPostAndUserAsync(post.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostLike?)null);

        var handler = new TogglePostLikeCommandHandler(
            _f.PostRepository.Object, _f.PostLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act
        await handler.Handle(new TogglePostLikeCommand(post.Id), CancellationToken.None);

        // Assert — realtime cho cả feed lẫn trang chi tiết
        _f.Notifier.Verify(n => n.NotifyFeedAsync("post-like-changed", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _f.Notifier.Verify(n => n.NotifyPostAsync(post.Id, "post-like-changed", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ================= TOGGLE POST BOOKMARK (UC-20 / FR-20) =================

    [Fact]
    public async Task ToggleBookmark_AddAndRemove_ShouldKeepCounterConsistent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        post.BookmarksCount = 0;
        var savedBookmark = new PostBookmark();

        _f.LoginAs(userId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new TogglePostBookmarkCommandHandler(
            _f.PostRepository.Object, _f.PostBookmarkRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Lần 1: chưa lưu → thêm, counter 0 → 1
        _f.PostBookmarkRepository.SetupSequence(r => r.GetByPostAndUserAsync(post.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostBookmark?)null)
            .ReturnsAsync(savedBookmark);

        var first = await handler.Handle(new TogglePostBookmarkCommand(post.Id), CancellationToken.None);
        Assert.True(first.Data!.IsBookmarked);
        Assert.Equal(1, first.Data.BookmarksCount);

        // Lần 2: đã lưu → xóa, counter 1 → 0
        var second = await handler.Handle(new TogglePostBookmarkCommand(post.Id), CancellationToken.None);
        Assert.False(second.Data!.IsBookmarked);
        Assert.Equal(0, second.Data.BookmarksCount);
        _f.PostBookmarkRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleBookmark_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new TogglePostBookmarkCommandHandler(
            _f.PostRepository.Object, _f.PostBookmarkRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Community.Application.Common.Exceptions.NotFoundException>(() =>
            handler.Handle(new TogglePostBookmarkCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ToggleBookmark_ShouldBroadcastToPostGroupOnly_NotFeed()
    {
        // Arrange — bookmark là hành động riêng tư: chỉ broadcast trang chi tiết
        var userId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        _f.LoginAs(userId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.PostBookmarkRepository.Setup(r => r.GetByPostAndUserAsync(post.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostBookmark?)null);

        var handler = new TogglePostBookmarkCommandHandler(
            _f.PostRepository.Object, _f.PostBookmarkRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        await handler.Handle(new TogglePostBookmarkCommand(post.Id), CancellationToken.None);

        // Assert
        _f.Notifier.Verify(n => n.NotifyPostAsync(post.Id, "post-bookmark-changed", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _f.Notifier.Verify(n => n.NotifyFeedAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ================= TOGGLE COMMENT LIKE (UC-28 / FR-28) =================

    [Fact]
    public async Task ToggleCommentLike_WhenNotLiked_ShouldAdd_AndNotifyCommentAuthor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var commentAuthorId = Guid.NewGuid();
        var comment = TestFixture.CreateComment(authorId: commentAuthorId);
        comment.LikesCount = 2;

        _f.LoginAs(userId, "bob");
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        _f.CommentLikeRepository.Setup(r => r.GetByCommentAndUserAsync(comment.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommentLike?)null);

        var handler = new ToggleCommentLikeCommandHandler(
            _f.CommentRepository.Object, _f.CommentLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(new ToggleCommentLikeCommand(comment.Id), CancellationToken.None);

        // Assert — 2 → 3 + thông báo cho tác giả bình luận
        Assert.True(result.Data!.IsLiked);
        Assert.Equal(3, result.Data.LikesCount);
        _f.NotificationClient.Verify(c => c.SendAsync(
            It.Is<NotificationOutbound>(n => n.RecipientId == commentAuthorId && n.Type == NotificationTypes.PostLiked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleCommentLike_WhenAlreadyLiked_ShouldRemove_NoNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var comment = TestFixture.CreateComment();
        comment.LikesCount = 3;
        var existingLike = new CommentLike { Id = Guid.NewGuid(), CommentId = comment.Id, UserId = userId };

        _f.LoginAs(userId, "bob");
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        _f.CommentLikeRepository.Setup(r => r.GetByCommentAndUserAsync(comment.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLike);

        var handler = new ToggleCommentLikeCommandHandler(
            _f.CommentRepository.Object, _f.CommentLikeRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(new ToggleCommentLikeCommand(comment.Id), CancellationToken.None);

        // Assert
        Assert.False(result.Data!.IsLiked);
        Assert.Equal(2, result.Data.LikesCount);
        _f.NotificationClient.Verify(c => c.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
