using Community.Application.Features.Posts.Commands.CreatePost;
using Community.Application.Interfaces;
using Community.Domain.Enums;
using Community.Domain.Entities;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.Posts;

/// <summary>
/// Unit test UC-15 / FR-15 (đăng bài viết): auth bắt buộc, AuthorId từ JWT,
/// kiểm duyệt AI (pass → Published, vi phạm → Hidden), counters init, realtime broadcast.
/// </summary>
public class CreatePostCommandHandlerTests
{
    private readonly TestFixture _f = new();

    [Fact]
    public async Task Handle_WithValidContent_ShouldCreatePublishedPost()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "alice");
        _f.Moderation.Setup(m => m.CheckAsync("Xin chào cộng đồng!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModerationResult { IsValid = true, Toxicity = "safe", QualityScore = 95 });

        var handler = new CreatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new CreatePostCommand { Content = "Xin chào cộng đồng!" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);

        _f.PostRepository.Verify(r => r.AddAsync(It.Is<Post>(p =>
            p.AuthorId == _f.CurrentUser.Object.UserId &&
            p.Status == (int)ContentStatus.Published &&
            p.LikesCount == 0 && p.CommentsCount == 0 &&
            p.SharesCount == 0 && p.ViewsCount == 0), It.IsAny<CancellationToken>()), Times.Once);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenContentViolatesModeration_ShouldCreateHiddenPost()
    {
        // Arrange — nội dung toxic → bài vẫn được tạo nhưng ẩn chờ Admin (SRS: độ an toàn)
        _f.LoginAs(Guid.NewGuid(), "alice");
        _f.Moderation.Setup(m => m.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModerationResult { IsValid = false, Toxicity = "toxic", QualityScore = 10 });

        var handler = new CreatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new CreatePostCommand { Content = "nội dung xấu" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess); // vẫn thành công, nhưng bị flag
        Assert.Contains("flagged", result.Message, StringComparison.OrdinalIgnoreCase);

        _f.PostRepository.Verify(r => r.AddAsync(It.Is<Post>(p =>
            p.Status == (int)ContentStatus.Hidden), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ShouldThrowUnauthorized()
    {
        // Arrange — Guest không được đăng bài
        _f.Logout();
        var handler = new CreatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new CreatePostCommand { Content = "hello" }, CancellationToken.None));

        _f.PostRepository.Verify(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseJwtAuthorId_IgnoringBodyAuthorId()
    {
        // Arrange — AuthorId trong body phải bị bỏ qua (chống đăng bài hộ)
        var jwtUserId = Guid.NewGuid();
        var fakeBodyAuthorId = Guid.NewGuid();
        _f.LoginAs(jwtUserId, "alice");

        var handler = new CreatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        await handler.Handle(new CreatePostCommand { Content = "test", AuthorId = fakeBodyAuthorId }, CancellationToken.None);

        // Assert — bài viết phải thuộc jwtUserId, KHÔNG phải body
        _f.PostRepository.Verify(r => r.AddAsync(It.Is<Post>(p =>
            p.AuthorId == jwtUserId && p.AuthorId != fakeBodyAuthorId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldBroadcastPostCreatedToFeed()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "alice");
        var handler = new CreatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act
        await handler.Handle(new CreatePostCommand { Content = "broadcast me" }, CancellationToken.None);

        // Assert — realtime event cho bảng tin
        _f.Notifier.Verify(n => n.NotifyFeedAsync("post-created", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenModerationThrows_ShouldPropagate()
    {
        // Arrange — QualityService chết → exception phải bubble up (không nuốt âm thầm)
        _f.LoginAs(Guid.NewGuid(), "alice");
        _f.Moderation.Setup(m => m.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("QualityService unavailable"));

        var handler = new CreatePostCommandHandler(
            _f.PostRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object,
            _f.Moderation.Object, _f.Notifier.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            handler.Handle(new CreatePostCommand { Content = "test" }, CancellationToken.None));

        _f.PostRepository.Verify(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
