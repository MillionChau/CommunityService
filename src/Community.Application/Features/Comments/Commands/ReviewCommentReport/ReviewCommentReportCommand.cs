using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.ReviewCommentReport;

public record ReviewCommentReportCommand : IRequest<ResponseModel<bool>>
{
    public Guid ReportId { get; init; }
    public bool Approve { get; init; }
    public string? ReviewNote { get; init; }
}

public class ReviewCommentReportCommandValidator : AbstractValidator<ReviewCommentReportCommand>
{
    public ReviewCommentReportCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty().WithMessage("ReportId is required.");
        RuleFor(x => x.ReviewNote)
            .MaximumLength(500).WithMessage("ReviewNote must not exceed 500 characters.");
    }
}

/// <summary>
/// Admin duyệt/từ chối báo cáo bình luận (UC-29 / UC-63).
/// - Approve = true: report -> Approved, bình luận bị ẩn (Status = Hidden).
/// - Approve = false: report -> Rejected.
/// </summary>
public class ReviewCommentReportCommandHandler : IRequestHandler<ReviewCommentReportCommand, ResponseModel<bool>>
{
    private readonly ICommentReportRepository _commentReportRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;
    private readonly IReputationService? _reputationService;

    public ReviewCommentReportCommandHandler(
        ICommentReportRepository commentReportRepository,
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        INotificationClient notificationClient,
        IReputationService? reputationService = null)
    {
        _commentReportRepository = commentReportRepository;
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
        _reputationService = reputationService;
    }

    public async Task<ResponseModel<bool>> Handle(ReviewCommentReportCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators can review reports.");

        var report = await _commentReportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report == null)
            throw new NotFoundException(nameof(CommentReport), request.ReportId);

        if (report.Status != (int)ReportStatus.Pending)
            return ResponseModel<bool>.Error("This comment report has already been reviewed.");

        report.Status = request.Approve ? (int)ReportStatus.Approved : (int)ReportStatus.Rejected;
        report.ReviewNote = request.ReviewNote;
        await _commentReportRepository.UpdateAsync(report, cancellationToken);

        // Nếu duyệt báo cáo -> ẩn bình luận vi phạm
        Guid? postId = null;
        if (request.Approve)
        {
            var comment = await _commentRepository.GetByIdAsync(report.CommentId, cancellationToken);
            if (comment != null)
            {
                comment.Status = (int)ContentStatus.Hidden;
                postId = comment.PostId;
                await _commentRepository.UpdateAsync(comment, cancellationToken);

                // SPEC-REP: Phạt trừ điểm khi bình luận bị báo cáo và Admin duyệt (-20 PRP, -10 TSS)
                if (comment.AuthorId.HasValue && _reputationService != null)
                {
                    await _reputationService.DeductPointsAsync(
                        comment.AuthorId.Value,
                        "REPORT_APPROVED_COMMENT",
                        pointsPenalty: 20,
                        tssPenalty: 10,
                        sourceId: comment.Id,
                        reason: $"Admin duyệt báo cáo vi phạm tiêu chuẩn cộng đồng: {report.Reason}",
                        cancellationToken);
                }
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        // Thông báo kết quả cho người báo cáo
        await _notificationClient.SendAsync(new NotificationOutbound
        {
            RecipientId = report.ReporterId,
            Type = request.Approve ? NotificationTypes.PostHidden : NotificationTypes.ReportReviewed,
            Content = request.Approve
                ? "Báo cáo của bạn về bình luận đã được chấp nhận — bình luận vi phạm đã bị ẩn."
                : "Báo cáo của bạn đã được xem xét — bình luận không vi phạm.",
            LinkUrl = postId.HasValue ? $"/posts/{postId.Value}" : "/posts",
            RelatedEntityId = report.Id,
            RelatedPostId = postId,
            ActorId = _currentUser.UserId,
            ActorName = _currentUser.UserName
        }, cancellationToken);

        await _notificationClient.ResolvePendingReportAsync(report.Id, cancellationToken);

        return ResponseModel<bool>.Success(true,
            request.Approve ? "Comment report approved. Comment has been hidden." : "Comment report rejected.");
    }
}
