using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Features.Posts.Commands.CreateComment;
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
/// Unit test UC-24/UC-25 (bình luận + trả lời bình luận): AuthorId từ JWT,
/// tăng CommentsCount, thông báo cho author bài/parent comment, không tự báo chính mình.
/// </summary>
public class CreateCommentCommandHandlerTests
{
    private readonly TestFixture _f = new();

    private CreateCommentCommandHandler BuildHandler()
        => new(
            _f.CommentRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object, _f.NotificationClient.Object, _f.Mapper);

    [Fact]
    public async Task Handle_ValidComment_ShouldCreate_AndIncrementPostCommentsCount()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var post = TestFixture.CreatePost(authorId: Guid.NewGuid());
        post.CommentsCount = 5;
        _f.LoginAs(authorId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // Act
        var result = await BuildHandler().Handle(
            new CreateCommentCommand { PostId = post.Id, Content = "Bình luận hay quá!" }, CancellationToken.None);

        // Assert — counter tăng 5 → 6, comment có AuthorId từ JWT
        Assert.True(result.IsSuccess);
        Assert.Equal(6, post.CommentsCount);
        _f.CommentRepository.Verify(r => r.AddAsync(It.Is<Comment>(c =>
            c.AuthorId == authorId && c.Content == "Bình luận hay quá!" && c.PostId == post.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ShouldThrowUnauthorized()
    {
        // Arrange
        _f.Logout();
        var handler = BuildHandler();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new CreateCommentCommand { PostId = Guid.NewGuid(), Content = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        await Assert.ThrowsAsync<Community.Application.Common.Exceptions.NotFoundException>(() =>
            BuildHandler().Handle(new CreateCommentCommand { PostId = Guid.NewGuid(), Content = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CommentOnOthersPost_ShouldNotifyPostAuthor()
    {
        // Arrange — bob comment bài của alice → alice nhận thông báo PostCommented
        var bobId = Guid.NewGuid();
        var aliceId = Guid.NewGuid();
        var post = TestFixture.CreatePost(authorId: aliceId);
        _f.LoginAs(bobId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // Act
        await BuildHandler().Handle(new CreateCommentCommand { PostId = post.Id, Content = "hi" }, CancellationToken.None);

        // Assert
        _f.NotificationClient.Verify(c => c.SendManyAsync(
            It.Is<IReadOnlyList<NotificationOutbound>>(list =>
                list.Count == 1 &&
                list[0].RecipientId == aliceId &&
                list[0].Type == NotificationTypes.PostCommented),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SelfComment_ShouldNotNotifySelf()
    {
        // Arrange — alice tự comment bài của alice → không có thông báo nào để gửi
        var aliceId = Guid.NewGuid();
        var post = TestFixture.CreatePost(authorId: aliceId);
        _f.LoginAs(aliceId, "alice");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // Act
        await BuildHandler().Handle(new CreateCommentCommand { PostId = post.Id, Content = "self" }, CancellationToken.None);

        // Assert — handler bỏ qua gửi khi list rỗng (không gọi SendManyAsync)
        _f.NotificationClient.Verify(c => c.SendManyAsync(
            It.IsAny<IReadOnlyList<NotificationOutbound>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReplyToOthersComment_ShouldNotifyParentAuthor_NotPostAuthorTwice()
    {
        // Arrange — carol reply comment của bob trong bài của alice:
        // bob nhận CommentReplied, alice nhận PostCommented (2 thông báo khác người)
        var bobId = Guid.NewGuid();   // author comment cha
        var aliceId = Guid.NewGuid(); // author bài viết
        var carolId = Guid.NewGuid();

        var post = TestFixture.CreatePost(authorId: aliceId);
        var parentComment = TestFixture.CreateComment(postId: post.Id, authorId: bobId);

        _f.LoginAs(carolId, "carol");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.CommentRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<Comment, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Comment, bool>>? expr, CancellationToken _) =>
            {
                var data = _f.ToAsyncQueryable(new List<Comment> { parentComment });
                return expr == null ? data : data.Where(expr);
            });

        // Act
        await BuildHandler().Handle(new CreateCommentCommand
        {
            PostId = post.Id,
            Content = "reply",
            ParentCommentId = parentComment.Id
        }, CancellationToken.None);

        // Assert — 2 thông báo: CommentReplied → bob, PostCommented → alice
        _f.NotificationClient.Verify(c => c.SendManyAsync(
            It.Is<IReadOnlyList<NotificationOutbound>>(list =>
                list.Count == 2 &&
                list.Any(n => n.RecipientId == bobId && n.Type == NotificationTypes.CommentReplied) &&
                list.Any(n => n.RecipientId == aliceId && n.Type == NotificationTypes.PostCommented)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
