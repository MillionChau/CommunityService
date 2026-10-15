using System.Linq.Expressions;
using Community.Application.Common.Exceptions;
using Community.Application.Features.BookmarkFolders.Commands.CreateBookmarkFolder;
using Community.Application.Features.BookmarkFolders.Commands.DeleteBookmarkFolder;
using Community.Application.Features.BookmarkFolders.Commands.UpdateBookmarkFolder;
using Community.Application.Features.BookmarkFolders.Queries.GetMyBookmarkFolders;
using Community.Application.Features.Posts.Commands.TogglePostBookmark;
using Community.Domain.Entities;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.BookmarkFolders;

public class BookmarkFoldersHandlerTests
{
    private readonly TestFixture _f = new();

    [Fact]
    public async Task CreateFolder_ByAuthenticatedUser_ShouldSucceed()
    {
        // Arrange (UC-21)
        var userId = Guid.NewGuid();
        _f.LoginAs(userId, "alice");

        var handler = new CreateBookmarkFolderCommandHandler(
            _f.BookmarkFolderRepository.Object, _f.UnitOfWork.Object, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(
            new CreateBookmarkFolderCommand { Name = "C# Tips", Description = "Best C# practices", ColorCode = "#512BD4" },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);
        _f.BookmarkFolderRepository.Verify(r => r.AddAsync(It.Is<BookmarkFolder>(f =>
            f.UserId == userId && f.Name == "C# Tips" && f.ColorCode == "#512BD4"), It.IsAny<CancellationToken>()), Times.Once);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyFolders_ShouldReturnUserFolders()
    {
        // Arrange (UC-21)
        var userId = Guid.NewGuid();
        _f.LoginAs(userId, "alice");

        var folders = new List<BookmarkFolder>
        {
            new() { Id = Guid.NewGuid(), Name = "Folder 1", UserId = userId },
            new() { Id = Guid.NewGuid(), Name = "Folder 2", UserId = userId }
        };

        _f.BookmarkFolderRepository
            .Setup(r => r.GetUserFoldersAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folders);

        _f.PostBookmarkRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostBookmark, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns(_f.ToEmptyAsyncQueryable<PostBookmark>());

        var handler = new GetMyBookmarkFoldersQueryHandler(
            _f.BookmarkFolderRepository.Object, _f.PostBookmarkRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetMyBookmarkFoldersQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task ToggleBookmark_WithFolderId_ShouldSaveWithFolder()
    {
        // Arrange (UC-20, UC-21)
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _f.LoginAs(userId, "alice");

        var post = TestFixture.CreatePost();
        post.Id = postId;
        _f.PostRepository.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _f.PostBookmarkRepository.Setup(r => r.GetByPostAndUserAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostBookmark?)null);

        var handler = new TogglePostBookmarkCommandHandler(
            _f.PostRepository.Object, _f.PostBookmarkRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.Notifier.Object);

        // Act
        var result = await handler.Handle(new TogglePostBookmarkCommand(postId, folderId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsBookmarked);
        _f.PostBookmarkRepository.Verify(r => r.AddAsync(It.Is<PostBookmark>(b =>
            b.PostId == postId && b.UserId == userId && b.FolderId == folderId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
