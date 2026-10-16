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
using Microsoft.EntityFrameworkCore;
using CommunityValidationException = Community.Application.Common.Exceptions.ValidationException;

namespace Community.Application.Features.Comments.Commands.CreateCommentReport;

public record CreateCommentReportCommand : IRequest<ResponseModel<CommentReportDto>>
{
    public Guid CommentId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class CreateCommentReportCommandValidator : AbstractValidator<CreateCommentReportCommand>
{
    public CreateCommentReportCommandValidator()
    {
        RuleFor(x => x.CommentId).NotEmpty().WithMessage("CommentId is required.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

/// <summary>
/// Báo cáo bình luận vi phạm (FR-29 / UC-29). ReporterId lấy từ JWT.
/// Ràng buộc: Mỗi người dùng chỉ được có MỘT báo cáo Pending cho mỗi bình luận.
/// </summary>
public class CreateCommentReportCommandHandler : IRequestHandler<CreateCommentReportCommand, ResponseModel<CommentReportDto>>
{
    private readonly ICommentReportRepository _commentReportRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;
    private readonly IMapper _mapper;

    public CreateCommentReportCommandHandler(
        ICommentReportRepository commentReportRepository,
        ICommentRepository commentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        INotificationClient notificationClient,
        IMapper mapper)
    {
        _commentReportRepository = commentReportRepository;
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
        _mapper = mapper;
    }

    public async Task<ResponseModel<CommentReportDto>> Handle(CreateCommentReportCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var reporterId = _currentUser.UserId.Value;

        // Bình luận phải tồn tại và chưa bị xóa
        var comment = await _commentRepository.GetByIdAsync(request.CommentId, cancellationToken);
        if (comment == null || comment.IsDeleted == 1)
            throw new NotFoundException(nameof(Comment), request.CommentId);

        // Chặn trùng báo cáo Pending
        var existing = await _commentReportRepository
            .GetByExpression(r => r.CommentId == request.CommentId
                                  && r.ReporterId == reporterId
                                  && r.Status == (int)ReportStatus.Pending)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            return ResponseModel<CommentReportDto>.Error(
                "You have already reported this comment. Please wait for the moderators to review it.");

        var commentReport = new CommentReport
        {
            Id = Guid.NewGuid(),
            CommentId = request.CommentId,
            ReporterId = reporterId,
            Reason = request.Reason.Trim(),
            Status = (int)ReportStatus.Pending
        };

        await _commentReportRepository.AddAsync(commentReport, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // Đẩy thông báo cho Admin Queue (Notification Service)
        await _notificationClient.SendAsync(new NotificationOutbound
        {
            RecipientId = NotificationTypes.AdminQueueRecipient,
            Type = NotificationTypes.PendingReport,
            Content = $"Báo cáo vi phạm bình luận từ {_currentUser.UserName ?? "người dùng"}: {request.Reason}",
            LinkUrl = $"/admin/reports/comments/{commentReport.Id}",
            SourceService = "community",
            RelatedEntityId = commentReport.Id,
            RelatedPostId = comment.PostId,
            ActorId = reporterId,
            ActorName = _currentUser.UserName
        }, cancellationToken);

        var dto = _mapper.Map<CommentReportDto>(commentReport);
        return ResponseModel<CommentReportDto>.Success(dto, "Comment report submitted successfully.");
    }
}
