using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.RestorePost;

public record RestorePostCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
}

public class RestorePostCommandValidator : AbstractValidator<RestorePostCommand>
{
    public RestorePostCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("PostId is required.");
    }
}

/// <summary>
/// Quản trị viên khôi phục bài viết đã bị ẩn (UC-55).
/// Chuyển trạng thái Status sang Published (1) và IsApproved = 1.
/// </summary>
public class RestorePostCommandHandler : IRequestHandler<RestorePostCommand, ResponseModel<bool>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;

    public RestorePostCommandHandler(
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

    public async Task<ResponseModel<bool>> Handle(RestorePostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can restore posts.");

        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        post.Status = (int)ContentStatus.Published;
        post.IsApproved = 1;
        post.IsDeleted = 0;
        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        if (post.AuthorId.HasValue)
        {
            await _notificationClient.SendAsync(new NotificationOutbound
            {
                RecipientId = post.AuthorId.Value,
                Type = NotificationTypes.ReportReviewed,
                Content = "Bài viết của bạn đã được Quản trị viên khôi phục và hiển thị trở lại.",
                LinkUrl = $"/posts/{post.Id}",
                RelatedEntityId = post.Id,
                RelatedPostId = post.Id
            }, cancellationToken);
        }

        return ResponseModel<bool>.Success(true, "Post has been restored successfully.");
    }
}
