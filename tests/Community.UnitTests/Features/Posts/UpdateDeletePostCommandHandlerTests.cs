using Community.Application.Common.Exceptions;
using Community.Application.Features.Posts.Commands.DeletePost;
using Community.Application.Features.Posts.Commands.UpdatePost;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.Posts;

/// <summary>
/// Unit test UC-16 (chỉnh sửa bài) + UC-17 (xóa bài): ownership check,
/// Admin bypass, kiểm duyệt lại khi sửa, soft delete theo SRS.
/// </summary>
public class UpdateDeletePostCommandHandlerTests
{
    private readonly TestFixture _f = new();

    // ================= UPDATE (UC-16 / FR-16) =================

    [Fact]
    public async Task Update_ByAuthor_ShouldSucceed_AndSetDraftForReModeration()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        _f.LoginAs(authorId, "alice");
        var post = TestFixture.CreatePost(authorId);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new UpdatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new UpdatePostCommand { Id = post.Id, Content = "updated content" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("updated content", post.Content);
        // Nội dung sửa phải quay về Draft chờ kiểm duyệt lại trước khi hiển thị
        Assert.Equal((int)ContentStatus.Draft, post.Status);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_ByNonAuthor_ShouldThrowForbidden()
    {
        // Arrange — user khác không được sửa bài người khác
        _f.LoginAs(Guid.NewGuid(), "mallory");
        var post = TestFixture.CreatePost(Guid.NewGuid());
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new UpdatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new UpdatePostCommand { Id = post.Id, Content = "hack" }, CancellationToken.None));
    }

    [Fact]
    public async Task Update_ByAdmin_ShouldBypassOwnership()
    {
        // Arrange — Admin được sửa mọi bài
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        var post = TestFixture.CreatePost(Guid.NewGuid());
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new UpdatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new UpdatePostCommand { Id = post.Id, Content = "admin fix" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_WhenContentViolatesModeration_ShouldReturnErrorWithoutSaving()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        _f.LoginAs(authorId, "alice");
        var post = TestFixture.CreatePost(authorId);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.Moderation.Setup(m => m.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModerationResult { IsValid = false, Toxicity = "toxic" });

        var handler = new UpdatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new UpdatePostCommand { Id = post.Id, Content = "bad" }, CancellationToken.None);

        // Assert — trả lỗi, không đổi nội dung, không save
        Assert.False(result.IsSuccess);
        Assert.NotEqual("bad", post.Content);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "alice");
        var handler = new UpdatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdatePostCommand { Id = Guid.NewGuid(), Content = "x" }, CancellationToken.None));
    }

    // ================= DELETE (UC-17 / FR-17) =================

    [Fact]
    public async Task Delete_ByAuthor_ShouldSoftDelete()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        _f.LoginAs(authorId, "alice");
        var post = TestFixture.CreatePost(authorId);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new DeletePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new DeletePostCommand { Id = post.Id }, CancellationToken.None);

        // Assert — soft delete: Status = Deleted, KHÔNG xóa vật lý
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ContentStatus.Deleted, post.Status);
        _f.PostRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _f.PostRepository.Verify(r => r.UpdateAsync(post, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ByNonAuthor_ShouldThrowForbidden()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "mallory");
        var post = TestFixture.CreatePost(Guid.NewGuid());
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new DeletePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new DeletePostCommand { Id = post.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ByAdmin_ShouldBypassOwnership()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        var post = TestFixture.CreatePost(Guid.NewGuid());
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new DeletePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new DeletePostCommand { Id = post.Id }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ContentStatus.Deleted, post.Status);
    }

    [Fact]
    public async Task Delete_ShouldBroadcastPostDeleted()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        _f.LoginAs(authorId, "alice");
        var post = TestFixture.CreatePost(authorId);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new DeletePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        await handler.Handle(new DeletePostCommand { Id = post.Id }, CancellationToken.None);

        // Assert — cả feed lẫn trang chi tiết phải nhận event
        _f.Notifier.Verify(n => n.NotifyFeedAsync("post-deleted", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _f.Notifier.Verify(n => n.NotifyPostAsync(post.Id, "post-deleted", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
