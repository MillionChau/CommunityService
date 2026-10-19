using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.HideComment;

public record HideCommentCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public string? Reason { get; init; }
}

public class HideCommentCommandValidator : AbstractValidator<HideCommentCommand>
{
    public HideCommentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("CommentId is required.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

/// <summary>
/// Quản trị viên chủ động ẩn bình luận vi phạm tiêu chuẩn (UC-59).
/// Chuyển trạng thái Status sang Hidden (2).
/// </summary>
public class HideCommentCommandHandler : IRequestHandler<HideCommentCommand, ResponseModel<bool>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;

    public HideCommentCommandHandler(
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

    public async Task<ResponseModel<bool>> Handle(HideCommentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can hide comments.");

        var comment = await _commentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (comment == null)
            throw new NotFoundException(nameof(Comment), request.Id);

        comment.Status = (int)ContentStatus.Hidden;
        await _commentRepository.UpdateAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        if (comment.AuthorId.HasValue)
        {
            var note = !string.IsNullOrWhiteSpace(request.Reason) ? $": {request.Reason}" : ".";
            await _notificationClient.SendAsync(new NotificationOutbound
            {
                RecipientId = comment.AuthorId.Value,
                Type = NotificationTypes.System,
                Content = $"Bình luận của bạn đã bị Quản trị viên ẩn do vi phạm quy định cộng đồng{note}",
                LinkUrl = comment.PostId.HasValue ? $"/posts/{comment.PostId}" : "/community",
                RelatedEntityId = comment.Id,
                RelatedPostId = comment.PostId
            }, cancellationToken);
        }

        return ResponseModel<bool>.Success(true, "Comment has been hidden successfully.");
    }
}
