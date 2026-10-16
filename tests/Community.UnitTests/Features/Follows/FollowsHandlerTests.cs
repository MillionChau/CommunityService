using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Application.Features.Follows.Commands.ToggleFollowUser;
using Community.Application.Features.Follows.Queries.GetFollowSummary;
using Community.Domain.Entities;
using Community.UnitTests.TestFixtures;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Community.UnitTests.Features.Follows;

public class FollowsHandlerTests
{
    private readonly TestFixture _f = new();

    [Fact]
    public async Task ToggleFollowUser_WhenFollowNewUser_ShouldSucceed_AndNotifyAuthor()
    {
        // Arrange
        var followerId = Guid.NewGuid();
        var targetAuthorId = Guid.NewGuid();

        _f.LoginAs(followerId, "john");

        var emptyList = _f.ToEmptyAsyncQueryable<UserFollow>();
        _f.UserFollowRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<UserFollow, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<UserFollow, bool>>? expr, CancellationToken _) =>
                expr == null ? emptyList : emptyList.Where(expr));

        _f.UserFollowRepository
            .Setup(r => r.GetFollowersCountAsync(targetAuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new ToggleFollowUserCommandHandler(
            _f.UserFollowRepository.Object,
            _f.UnitOfWork.Object,
            _f.CurrentUser.Object,
            _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(new ToggleFollowUserCommand(targetAuthorId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsFollowing);
        Assert.Equal(1, result.Data!.FollowersCount);
        _f.UserFollowRepository.Verify(r => r.AddAsync(It.IsAny<UserFollow>(), It.IsAny<CancellationToken>()), Times.Once);
        _f.NotificationClient.Verify(n => n.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleFollowUser_WhenSelfFollow_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _f.LoginAs(userId, "john");

        var handler = new ToggleFollowUserCommandHandler(
            _f.UserFollowRepository.Object,
            _f.UnitOfWork.Object,
            _f.CurrentUser.Object,
            _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(new ToggleFollowUserCommand(userId), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("không thể tự theo dõi", result.Message);
        _f.UserFollowRepository.Verify(r => r.AddAsync(It.IsAny<UserFollow>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFollowSummary_ShouldReturnCorrectCounts()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        _f.LoginAs(currentUserId, "viewer");

        _f.UserFollowRepository
            .Setup(r => r.GetFollowersCountAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _f.UserFollowRepository
            .Setup(r => r.GetFollowingCountAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);
        _f.UserFollowRepository
            .Setup(r => r.IsFollowingAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new GetFollowSummaryQueryHandler(
            _f.UserFollowRepository.Object,
            _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetFollowSummaryQuery(targetUserId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Data!.FollowersCount);
        Assert.Equal(12, result.Data!.FollowingCount);
        Assert.True(result.Data!.IsFollowing);
    }
}
