using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Features.Comments.Commands.DeleteComment;
using Community.Application.Features.Comments.Queries.GetCommentsByPost;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Community.UnitTests.Features.Comments;

/// <summary>
/// Unit test UC-27 (xóa bình luận — soft delete + giảm counter, chỉ author/Admin)
/// và ngữ cảnh đọc bình luận của bài viết (FR-24): ẩn Deleted/Hidden, Admin thấy Hidden.
/// </summary>
public class DeleteAndGetCommentsHandlerTests
{
    private readonly TestFixture _f = new();

    // ================= DELETE COMMENT (UC-27 / FR-27) =================

    [Fact]
    public async Task DeleteComment_ByAuthor_ShouldSoftDelete_AndDecrementCounter()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        post.CommentsCount = 3;
        var comment = TestFixture.CreateComment(postId: post.Id, authorId: authorId);

        _f.LoginAs(authorId, "bob");
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new DeleteCommentCommandHandler(
            _f.CommentRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new DeleteCommentCommand { Id = comment.Id }, CancellationToken.None);

        // Assert — soft delete + counter 3 → 2
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ContentStatus.Deleted, comment.Status);
        Assert.Equal(2, post.CommentsCount);
        _f.CommentRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteComment_ByAdmin_ShouldSucceed()
    {
        // Arrange — Admin xóa comment vi phạm của người khác
        var comment = TestFixture.CreateComment(authorId: Guid.NewGuid());
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        var handler = new DeleteCommentCommandHandler(
            _f.CommentRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new DeleteCommentCommand { Id = comment.Id }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ContentStatus.Deleted, comment.Status);
    }

    [Fact]
    public async Task DeleteComment_ByNonAuthor_ShouldThrowForbidden()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "mallory");
        var comment = TestFixture.CreateComment(authorId: Guid.NewGuid());
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        var handler = new DeleteCommentCommandHandler(
            _f.CommentRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new DeleteCommentCommand { Id = comment.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteComment_ShouldBroadcastCommentDeleted()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        var comment = TestFixture.CreateComment(postId: post.Id, authorId: authorId);

        _f.LoginAs(authorId, "bob");
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new DeleteCommentCommandHandler(
            _f.CommentRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        await handler.Handle(new DeleteCommentCommand { Id = comment.Id }, CancellationToken.None);

        // Assert
        _f.Notifier.Verify(n => n.NotifyPostAsync(post.Id, "comment-deleted", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ================= GET COMMENTS BY POST (FR-24) =================

    private GetCommentsByPostQueryHandler BuildCommentsQueryHandler(List<Comment> comments)
    {
        _f.CommentRepository
            .Setup(r => r.GetCommentsByPostIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);

        var likes = _f.ToEmptyAsyncQueryable<CommentLike>();
        _f.CommentLikeRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<CommentLike, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<CommentLike, bool>>? expr, CancellationToken _) =>
                expr == null ? likes : likes.Where(expr));

        return new GetCommentsByPostQueryHandler(
            _f.CommentRepository.Object, _f.PostRepository.Object, _f.CommentLikeRepository.Object,
            _f.Mapper, _f.CurrentUser.Object);
    }

    [Fact]
    public async Task GetComments_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.Logout();
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildCommentsQueryHandler(new List<Comment>()).Handle(
                new GetCommentsByPostQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetComments_AsUser_ShouldHideDeletedAndHiddenComments()
    {
        // Arrange — 3 comment: published + hidden + deleted → user chỉ thấy published
        var post = TestFixture.CreatePost();
        var comments = new List<Comment>
        {
            TestFixture.CreateComment(postId: post.Id, status: (int)ContentStatus.Published),
            TestFixture.CreateComment(postId: post.Id, status: (int)ContentStatus.Hidden),
            TestFixture.CreateComment(postId: post.Id, status: (int)ContentStatus.Deleted)
        };

        _f.LoginAs(Guid.NewGuid(), "user");
        var handler = BuildCommentsQueryHandler(comments);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // Act
        var result = await handler.Handle(new GetCommentsByPostQuery(post.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetComments_AsAdmin_ShouldSeeHiddenButNotDeleted()
    {
        // Arrange — Admin thấy được Hidden (kiểm duyệt) nhưng vẫn không thấy Deleted
        var post = TestFixture.CreatePost();
        var comments = new List<Comment>
        {
            TestFixture.CreateComment(postId: post.Id, status: (int)ContentStatus.Published),
            TestFixture.CreateComment(postId: post.Id, status: (int)ContentStatus.Hidden),
            TestFixture.CreateComment(postId: post.Id, status: (int)ContentStatus.Deleted)
        };

        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        var handler = BuildCommentsQueryHandler(comments);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // Act
        var result = await handler.Handle(new GetCommentsByPostQuery(post.Id), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task GetComments_WhenPostIsDeleted_ShouldThrowNotFound()
    {
        // Arrange — bài đã xóa thì không xem comment nữa
        var post = TestFixture.CreatePost(status: (int)ContentStatus.Deleted);
        _f.LoginAs(Guid.NewGuid(), "user");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildCommentsQueryHandler(new List<Comment>()).Handle(
                new GetCommentsByPostQuery(post.Id), CancellationToken.None));
    }
}
