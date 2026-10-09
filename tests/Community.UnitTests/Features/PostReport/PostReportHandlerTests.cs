using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Features.PostReport.Commands.CreatePostReport;
using Community.Application.Features.PostReport.Commands.ReviewPostReport;
using Community.Application.Features.PostReport.Queries.GetPendingReports;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.UnitTests.TestFixtures;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Community.UnitTests.Features.PostReports;

/// <summary>
/// Unit test UC-23 (báo cáo vi phạm + hàng chờ duyệt): chặn report trùng,
/// chỉ Admin duyệt, duyệt → ẩn bài, thông báo kết quả cho reporter, queue cũ nhất trước.
/// </summary>
public class PostReportHandlerTests
{
    private readonly TestFixture _f = new();

    // ================= CREATE REPORT (FR-23 / UC-23) =================

    [Fact]
    public async Task CreateReport_ValidRequest_ShouldCreatePendingReport_AndNotifyAdminQueue()
    {
        // Arrange
        var reporterId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        _f.LoginAs(reporterId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existingReports = _f.ToEmptyAsyncQueryable<PostReport>();
        _f.PostReportRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostReport, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostReport, bool>>? expr, CancellationToken _) =>
                expr == null ? existingReports : existingReports.Where(expr));

        var handler = new CreatePostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(
            new CreatePostReportCommand { PostId = post.Id, Reason = "Spam link" }, CancellationToken.None);

        // Assert — report Pending + đẩy vào hàng chờ Admin (PendingReport notification)
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ReportStatus.Pending, result.Data!.Status);

        _f.NotificationClient.Verify(c => c.SendAsync(
            It.Is<NotificationOutbound>(n =>
                n.Type == NotificationTypes.PendingReport &&
                n.RecipientId == NotificationTypes.AdminQueueRecipient &&
                n.RelatedEntityId == result.Data.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateReport_DuplicatePendingReport_ShouldReturnError()
    {
        // Arrange — user đã có 1 report Pending cho bài này
        var reporterId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        var existing = TestFixture.CreateReport(postId: post.Id, reporterId: reporterId, status: (int)ReportStatus.Pending);

        _f.LoginAs(reporterId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existingReports = _f.ToAsyncQueryable(new List<PostReport> { existing });
        _f.PostReportRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostReport, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostReport, bool>>? expr, CancellationToken _) =>
                expr == null ? existingReports : existingReports.Where(expr));

        var handler = new CreatePostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(
            new CreatePostReportCommand { PostId = post.Id, Reason = "lại spam" }, CancellationToken.None);

        // Assert — bị chặn, KHÔNG thêm report mới, KHÔNG notify
        Assert.False(result.IsSuccess);
        Assert.Contains("already reported", result.Message, StringComparison.OrdinalIgnoreCase);
        _f.PostReportRepository.Verify(r => r.AddAsync(It.IsAny<PostReport>(), It.IsAny<CancellationToken>()), Times.Never);
        _f.NotificationClient.Verify(c => c.SendAsync(It.IsAny<NotificationOutbound>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateReport_AfterPreviousReportResolved_ShouldAllowNewReport()
    {
        // Arrange — report cũ đã Approved (không còn Pending) → được report lại
        var reporterId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        var resolved = TestFixture.CreateReport(postId: post.Id, reporterId: reporterId, status: (int)ReportStatus.Approved);

        _f.LoginAs(reporterId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existingReports = _f.ToAsyncQueryable(new List<PostReport> { resolved });
        _f.PostReportRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostReport, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostReport, bool>>? expr, CancellationToken _) =>
                expr == null ? existingReports : existingReports.Where(expr));

        var handler = new CreatePostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(
            new CreatePostReportCommand { PostId = post.Id, Reason = "vẫn spam" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _f.PostReportRepository.Verify(r => r.AddAsync(It.Is<PostReport>(r2 =>
            r2.Status == (int)ReportStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateReport_WhenPostNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new CreatePostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object, _f.Mapper);

        // Act & Assert
        await Assert.ThrowsAsync<Community.Application.Common.Exceptions.NotFoundException>(() =>
            handler.Handle(new CreatePostReportCommand { PostId = Guid.NewGuid(), Reason = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateReport_WhenNotAuthenticated_ShouldThrowUnauthorized()
    {
        // Arrange
        _f.Logout();
        var handler = new CreatePostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object, _f.Mapper);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new CreatePostReportCommand { PostId = Guid.NewGuid(), Reason = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateReport_DuplicateCheck_CorrectlyScopesToSameUserAndPost()
    {
        // Arrange — report Pending của user KHÁC trên cùng bài → không chặn
        var reporterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        var otherPending = TestFixture.CreateReport(postId: post.Id, reporterId: otherUserId, status: (int)ReportStatus.Pending);

        _f.LoginAs(reporterId, "bob");
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existingReports = _f.ToAsyncQueryable(new List<PostReport> { otherPending });
        _f.PostReportRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostReport, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostReport, bool>>? expr, CancellationToken _) =>
                expr == null ? existingReports : existingReports.Where(expr));

        var handler = new CreatePostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(
            new CreatePostReportCommand { PostId = post.Id, Reason = "report của tôi" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    // ================= REVIEW REPORT (UC-23 — Admin) =================

    [Fact]
    public async Task Review_Approve_ShouldHidePost_AndNotifyReporter_AndResolveQueue()
    {
        // Arrange — Admin duyệt report → bài bị ẩn, reporter nhận PostHidden, queue được resolve
        var adminId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var post = TestFixture.CreatePost();
        var report = TestFixture.CreateReport(postId: post.Id, reporterId: reporterId, status: (int)ReportStatus.Pending);

        _f.LoginAs(adminId, "admin", isAdmin: true);
        _f.PostReportRepository.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(
            new ReviewPostReportCommand { ReportId = report.Id, Approve = true, ReviewNote = "Xác nhận vi phạm" },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ReportStatus.Approved, report.Status);
        Assert.Equal("Xác nhận vi phạm", report.ReviewNote);
        Assert.Equal((int)ContentStatus.Hidden, post.Status); // bài vi phạm bị ẩn

        _f.NotificationClient.Verify(c => c.SendAsync(
            It.Is<NotificationOutbound>(n =>
                n.RecipientId == reporterId && n.Type == NotificationTypes.PostHidden),
            It.IsAny<CancellationToken>()), Times.Once);
        _f.NotificationClient.Verify(c => c.ResolvePendingReportAsync(report.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Review_Reject_ShouldKeepPostVisible_AndNotifyReportReviewed()
    {
        // Arrange — Admin từ chối report → bài giữ nguyên Published
        var adminId = Guid.NewGuid();
        var post = TestFixture.CreatePost(status: (int)ContentStatus.Published);
        var report = TestFixture.CreateReport(postId: post.Id, status: (int)ReportStatus.Pending);

        _f.LoginAs(adminId, "admin", isAdmin: true);
        _f.PostReportRepository.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(
            new ReviewPostReportCommand { ReportId = report.Id, Approve = false }, CancellationToken.None);

        // Assert — báo Fromchối: status Rejected, bài vẫn Published
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ReportStatus.Rejected, report.Status);
        Assert.Equal((int)ContentStatus.Published, post.Status);

        _f.NotificationClient.Verify(c => c.SendAsync(
            It.Is<NotificationOutbound>(n =>
                n.RecipientId == report.ReporterId && n.Type == NotificationTypes.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Review_ByNonAdmin_ShouldThrowUnauthorized()
    {
        // Arrange — user thường không được duyệt
        _f.LoginAs(Guid.NewGuid(), "user", isAdmin: false);
        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ReviewPostReportCommand { ReportId = Guid.NewGuid(), Approve = true }, CancellationToken.None));
    }

    [Fact]
    public async Task Review_WhenReportNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        _f.PostReportRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostReport?)null);

        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ReviewPostReportCommand { ReportId = Guid.NewGuid(), Approve = true }, CancellationToken.None));
    }

    [Fact]
    public async Task Review_AlreadyReviewedReport_ShouldReturnErrorWithoutDoubleProcessing()
    {
        // Arrange — report đã Approved trước đó → chặn duyệt 2 lần
        var report = TestFixture.CreateReport(status: (int)ReportStatus.Approved);
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        _f.PostReportRepository.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(
            new ReviewPostReportCommand { ReportId = report.Id, Approve = true }, CancellationToken.None);

        // Assert — trả lỗi, KHÔNG lưu gì thêm
        Assert.False(result.IsSuccess);
        Assert.Contains("already been reviewed", result.Message, StringComparison.OrdinalIgnoreCase);
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Review_Approve_WhenPostAlreadyDeleted_ShouldStillSucceed()
    {
        // Arrange — bài đã bị xóa trước khi duyệt → không crash, vẫn duyệt report
        var report = TestFixture.CreateReport(status: (int)ReportStatus.Pending);
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        _f.PostReportRepository.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _f.PostRepository.Setup(r => r.GetByIdAsync(report.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act
        var result = await handler.Handle(
            new ReviewPostReportCommand { ReportId = report.Id, Approve = true }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((int)ReportStatus.Approved, report.Status);
    }

    [Fact]
    public async Task Review_Approve_ShouldCommitReportAndPostInSingleSave()
    {
        // Arrange — report + post phải commit trong MỘT transaction
        var report = TestFixture.CreateReport(status: (int)ReportStatus.Pending);
        var post = TestFixture.CreatePost();
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        _f.PostReportRepository.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _f.PostRepository.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var handler = new ReviewPostReportCommandHandler(
            _f.PostReportRepository.Object, _f.PostRepository.Object, _f.UnitOfWork.Object,
            _f.CurrentUser.Object, _f.NotificationClient.Object);

        // Act
        await handler.Handle(
            new ReviewPostReportCommand { ReportId = report.Id, Approve = true }, CancellationToken.None);

        // Assert — SaveAsync gọi đúng 1 lần cho cả 2 thay đổi
        _f.UnitOfWork.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ================= GET PENDING REPORTS (Admin queue) =================

    private void SetupReportQueryable(List<PostReport> reports)
    {
        var data = _f.ToAsyncQueryable(reports);
        _f.PostReportRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<PostReport, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<PostReport, bool>>? expr, CancellationToken _) =>
                expr == null ? data : data.Where(expr));
    }

    [Fact]
    public async Task GetPendingReports_ByNonAdmin_ShouldThrowUnauthorized()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "user", isAdmin: false);
        SetupReportQueryable(new List<PostReport>());

        var handler = new GetPendingReportsQueryHandler(
            _f.PostReportRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetPendingReportsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetPendingReports_AsAdmin_ShouldReturnOnlyPending()
    {
        // Arrange — queue chỉ chứa Pending, loại Approved/Rejected
        var pendingReports = new List<PostReport>
        {
            TestFixture.CreateReport(status: (int)ReportStatus.Pending),
            TestFixture.CreateReport(status: (int)ReportStatus.Pending)
        };
        var all = new List<PostReport>
        {
            pendingReports[0], pendingReports[1],
            TestFixture.CreateReport(status: (int)ReportStatus.Approved),
            TestFixture.CreateReport(status: (int)ReportStatus.Rejected)
        };

        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        SetupReportQueryable(all);

        var handler = new GetPendingReportsQueryHandler(
            _f.PostReportRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetPendingReportsQuery { Page = 1, PageSize = 20 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetPendingReports_ShouldOrderOldestFirst_AndPaginate()
    {
        // Arrange — 3 pending với CreatedDate khác nhau, pageSize 2 → trang 1 lấy 2 cũ nhất
        var oldest = TestFixture.CreateReport(status: (int)ReportStatus.Pending);
        oldest.CreatedDate = DateTime.UtcNow.AddDays(-3);
        var middle = TestFixture.CreateReport(status: (int)ReportStatus.Pending);
        middle.CreatedDate = DateTime.UtcNow.AddDays(-2);
        var newest = TestFixture.CreateReport(status: (int)ReportStatus.Pending);
        newest.CreatedDate = DateTime.UtcNow.AddDays(-1);

        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        SetupReportQueryable(new List<PostReport> { oldest, middle, newest });

        var handler = new GetPendingReportsQueryHandler(
            _f.PostReportRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetPendingReportsQuery { Page = 1, PageSize = 2 }, CancellationToken.None);

        // Assert — cũ nhất lên đầu (UC-23: báo cáo lâu chưa xử lý ưu tiên)
        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(oldest.Id, items[0].Id);
        Assert.Equal(middle.Id, items[1].Id);
    }

    [Fact]
    public async Task GetPendingReports_EmptyQueue_ShouldReturnEmptyPagedResponse()
    {
        // Arrange
        _f.LoginAs(Guid.NewGuid(), "admin", isAdmin: true);
        SetupReportQueryable(new List<PostReport>());

        var handler = new GetPendingReportsQueryHandler(
            _f.PostReportRepository.Object, _f.Mapper, _f.CurrentUser.Object);

        // Act
        var result = await handler.Handle(new GetPendingReportsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }
}
