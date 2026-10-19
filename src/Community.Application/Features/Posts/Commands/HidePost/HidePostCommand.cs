using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.HidePost;

public record HidePostCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public string? Reason { get; init; }
}

public class HidePostCommandValidator : AbstractValidator<HidePostCommand>
{
    public HidePostCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

/// <summary>
/// Quản trị viên chủ động ẩn bài viết vi phạm quy chuẩn cộng đồng (UC-54).
/// Chuyển trạng thái Status sang Hidden (2) và gửi thông báo tới tác giả.
/// </summary>
public class HidePostCommandHandler : IRequestHandler<HidePostCommand, ResponseModel<bool>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;

    public HidePostCommandHandler(
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        INotificationClient notificationClient)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
    }

    public async Task<ResponseModel<bool>> Handle(HidePostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can hide posts directly.");

        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        post.Status = (int)ContentStatus.Hidden;
        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // Gửi thông báo cho tác giả nếu có AuthorId
        if (post.AuthorId.HasValue)
        {
            var note = !string.IsNullOrWhiteSpace(request.Reason) ? $": {request.Reason}" : ".";
            await _notificationClient.SendAsync(new NotificationOutbound
            {
                RecipientId = post.AuthorId.Value,
                Type = NotificationTypes.PostHidden,
                Content = $"Bài viết của bạn đã bị Quản trị viên ẩn do vi phạm tiêu chuẩn cộng đồng{note}",
                LinkUrl = $"/posts/{post.Id}",
                RelatedEntityId = post.Id,
                RelatedPostId = post.Id
            }, cancellationToken);
        }

        return ResponseModel<bool>.Success(true, "Post has been hidden successfully.");
    }
}
