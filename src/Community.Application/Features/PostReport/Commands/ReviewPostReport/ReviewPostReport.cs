using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.PostReport.Commands.ReviewPostReport;

public record ReviewPostReportCommand : IRequest<ResponseModel<bool>>
{
    public Guid ReportId { get; init; }
    /// <summary>true = duyệt (ẩn bài vi phạm), false = từ chối báo cáo.</summary>
    public bool Approve { get; init; }
    /// <summary>Ghi chú của Admin (tùy chọn).</summary>
    public string? ReviewNote { get; init; }
}

public class ReviewPostReportCommandValidator : AbstractValidator<ReviewPostReportCommand>
{
    public ReviewPostReportCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty().WithMessage("ReportId is required.");
        RuleFor(x => x.ReviewNote)
            .MaximumLength(500).WithMessage("ReviewNote must not exceed 500 characters.");
    }
}

/// <summary>
/// Admin duyệt/từ chối báo cáo bài viết (UC-23, màn hình "Hàng chờ duyệt nội dung").
/// - Approve = true: report → Resolved (Approved), bài viết bị ẩn (Status = Hidden).
/// - Approve = false: report → Resolved (Rejected), bài viết giữ nguyên.
/// Mọi thay đổi báo cáo + bài viết commit trong MỘT transaction (IUnitOfWork.SaveAsync).
/// </summary>
public class ReviewPostReportCommandHandler : IRequestHandler<ReviewPostReportCommand, ResponseModel<bool>>
{
    private readonly IPostReportRepository _postReportRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;

    public ReviewPostReportCommandHandler(
        IPostReportRepository postReportRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        INotificationClient notificationClient)
    {
        _postReportRepository = postReportRepository;
        _postRepository = postRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
    }

    public async Task<ResponseModel<bool>> Handle(ReviewPostReportCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators can review reports.");

        var report = await _postReportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report == null)
            throw new NotFoundException(nameof(PostReport), request.ReportId);

        if (report.Status != (int)ReportStatus.Pending)
            return ResponseModel<bool>.Error("This report has already been reviewed.");

        report.Status = request.Approve ? (int)ReportStatus.Approved : (int)ReportStatus.Rejected;
        report.ReviewNote = request.ReviewNote;
        await _postReportRepository.UpdateAsync(report, cancellationToken);

        // Duyệt báo cáo → ẩn bài viết vi phạm (SRS: "ẩn nội dung vi phạm")
        if (request.Approve)
        {
            var post = await _postRepository.GetByIdAsync(report.PostId, cancellationToken);
            if (post != null)
            {
                post.Status = (int)ContentStatus.Hidden;
                await _postRepository.UpdateAsync(post, cancellationToken);
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        // ===== Thông báo kết quả duyệt cho reporter (FR-29 + UC-23) =====
        await _notificationClient.SendAsync(new NotificationOutbound
        {
            RecipientId = report.ReporterId,
            Type = request.Approve ? NotificationTypes.PostHidden : NotificationTypes.ReportReviewed,
            Content = request.Approve
                ? $"Báo cáo của bạn về bài viết đã được chấp nhận — bài viết đã bị ẩn."
                : "Báo cáo của bạn đã được xem xét — bài viết không vi phạm.",
            LinkUrl = $"/posts/{report.PostId}",
            RelatedEntityId = report.Id,
            RelatedPostId = report.PostId,
            ActorId = _currentUser.UserId,
            ActorName = _currentUser.UserName
        }, cancellationToken);

        // Gỡ báo cáo khỏi hàng chờ PendingReport của NotificationService
        await _notificationClient.ResolvePendingReportAsync(report.Id, cancellationToken);

        return ResponseModel<bool>.Success(true,
            request.Approve ? "Report approved. Post has been hidden." : "Report rejected.");
    }
}
