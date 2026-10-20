using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.RestoreComment;

public record RestoreCommentCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
}

public class RestoreCommentCommandValidator : AbstractValidator<RestoreCommentCommand>
{
    public RestoreCommentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("CommentId is required.");
    }
}

/// <summary>
/// Quản trị viên khôi phục bình luận đã bị ẩn (UC-60).
/// Chuyển trạng thái Status sang Published (1) và IsApproved = 1.
/// </summary>
public class RestoreCommentCommandHandler : IRequestHandler<RestoreCommentCommand, ResponseModel<bool>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;

    public RestoreCommentCommandHandler(
        ICommentRepository commentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        INotificationClient notificationClient)
    {
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
    }

    public async Task<ResponseModel<bool>> Handle(RestoreCommentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can restore comments.");

        var comment = await _commentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (comment == null)
            throw new NotFoundException(nameof(Comment), request.Id);

        comment.Status = (int)ContentStatus.Published;
        comment.IsApproved = 1;
        comment.IsDeleted = 0;
        await _commentRepository.UpdateAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        if (comment.AuthorId.HasValue)
        {
            await _notificationClient.SendAsync(new NotificationOutbound
            {
                RecipientId = comment.AuthorId.Value,
                Type = NotificationTypes.ReportReviewed,
                Content = "Bình luận của bạn đã được Quản trị viên khôi phục hiển thị.",
                LinkUrl = comment.PostId.HasValue ? $"/posts/{comment.PostId}" : "/community",
                RelatedEntityId = comment.Id,
                RelatedPostId = comment.PostId
            }, cancellationToken);
        }

        return ResponseModel<bool>.Success(true, "Comment has been restored successfully.");
    }
}
