using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Application.Features.Comments.Commands.CreateCommentReport;
using Community.Application.Features.Comments.Commands.ReviewCommentReport;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Community.UnitTests.Features.Comments;

public class CommentReportHandlerTests
{
    private readonly TestFixture _f = new();

    [Fact]
    public async Task CreateCommentReport_WhenValid_ShouldSucceed_AndNotifyAdmin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var comment = TestFixture.CreateComment();
        _f.LoginAs(userId, "reporter");

        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var emptyList = _f.ToEmptyAsyncQueryable<CommentReport>();
        _f.CommentReportRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<CommentReport, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<CommentReport, bool>>? expr, CancellationToken _) =>
                expr == null ? emptyList : emptyList.Where(expr));

        var handler = new CreateCommentReportCommandHandler(
            _f.CommentReportRepository.Object,
            _f.CommentRepository.Object,
            _f.UnitOfWork.Object,
            _f.CurrentUser.Object,
            _f.NotificationClient.Object,
            _f.Mapper);

        // Act
        var result = await handler.Handle(
            new CreateCommentReportCommand { CommentId = comment.Id, Reason = "Nội dung phản cảm" },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ReportStatus.Pending, result.Data!.Status);
        _f.CommentReportRepository.Verify(r => r.AddAsync(It.IsAny<CommentReport>(), It.IsAny<CancellationToken>()), Times.Once);
        _f.NotificationClient.Verify(n => n.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewCommentReport_WhenApproved_ShouldHideComment_AndNotifyReporter()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var comment = TestFixture.CreateComment(status: (int)ContentStatus.Published);
        var report = new CommentReport
        {
            Id = Guid.NewGuid(),
            CommentId = comment.Id,
            ReporterId = reporterId,
            Reason = "Vi phạm",
            Status = (int)ReportStatus.Pending
        };

        _f.LoginAs(adminId, "admin", isAdmin: true);

        _f.CommentReportRepository.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _f.CommentRepository.Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var handler = new ReviewCommentReportCommandHandler(
            _f.CommentReportRepository.Object,
            _f.CommentRepository.Object,
            _f.PostRepository.Object,
            _f.UnitOfWork.Object,
            _f.CurrentUser.Object,
            _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(
            new ReviewCommentReportCommand { ReportId = report.Id, Approve = true, ReviewNote = "Đã ẩn" },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ReportStatus.Approved, report.Status);
        Assert.Equal((int)ContentStatus.Hidden, comment.Status);
        _f.CommentRepository.Verify(r => r.UpdateAsync(comment, It.IsAny<CancellationToken>()), Times.Once);
        _f.NotificationClient.Verify(n => n.ResolvePendingReportAsync(report.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
