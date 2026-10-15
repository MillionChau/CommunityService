using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CommunityValidationException = Community.Application.Common.Exceptions.ValidationException;

namespace Community.Application.Features.Follows.Commands.ToggleFollowUser;

public record ToggleFollowUserCommand(Guid TargetUserId) : IRequest<ResponseModel<ToggleFollowResult>>;

public record ToggleFollowResult(bool IsFollowing, int FollowersCount);

public class ToggleFollowUserCommandValidator : AbstractValidator<ToggleFollowUserCommand>
{
    public ToggleFollowUserCommandValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("TargetUserId is required.");
    }
}

/// <summary>
/// Bật / tắt theo dõi một tác giả (UC-30).
/// - Không cho phép người dùng tự theo dõi chính mình.
/// - Gửi thông báo đến người được theo dõi khi kích hoạt follow.
/// </summary>
public class ToggleFollowUserCommandHandler : IRequestHandler<ToggleFollowUserCommand, ResponseModel<ToggleFollowResult>>
{
    private readonly IUserFollowRepository _userFollowRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationClient _notificationClient;

    public ToggleFollowUserCommandHandler(
        IUserFollowRepository userFollowRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        INotificationClient notificationClient)
    {
        _userFollowRepository = userFollowRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notificationClient = notificationClient;
    }

    public async Task<ResponseModel<ToggleFollowResult>> Handle(ToggleFollowUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var followerId = _currentUser.UserId.Value;
        if (followerId == request.TargetUserId)
            return ResponseModel<ToggleFollowResult>.Error("Bạn không thể tự theo dõi chính mình.");

        var existingFollow = await _userFollowRepository
            .GetByExpression(f => f.FollowerId == followerId && f.FolloweeId == request.TargetUserId)
            .FirstOrDefaultAsync(cancellationToken);

        bool isFollowing;
        if (existingFollow != null)
        {
            // Đang follow -> Bỏ follow
            await _userFollowRepository.DeleteAsync(existingFollow.Id, cancellationToken);
            isFollowing = false;
        }
        else
        {
            // Chưa follow -> Tạo mới
            var newFollow = new UserFollow
            {
                Id = Guid.NewGuid(),
                FollowerId = followerId,
                FolloweeId = request.TargetUserId
            };
            await _userFollowRepository.AddAsync(newFollow, cancellationToken);
            isFollowing = true;

            // Gửi thông báo cho tác giả được follow
            await _notificationClient.SendAsync(new NotificationOutbound
            {
                RecipientId = request.TargetUserId,
                Type = NotificationTypes.AuthorFollowed,
                Content = $"{_currentUser.UserName ?? "Ai đó"} đã bắt đầu theo dõi bạn.",
                LinkUrl = $"/users/{followerId}",
                RelatedEntityId = newFollow.Id,
                ActorId = followerId,
                ActorName = _currentUser.UserName
            }, cancellationToken);
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        var followersCount = await _userFollowRepository.GetFollowersCountAsync(request.TargetUserId, cancellationToken);

        return ResponseModel<ToggleFollowResult>.Success(
            new ToggleFollowResult(isFollowing, followersCount),
            isFollowing ? "Đã theo dõi tác giả." : "Đã hủy theo dõi tác giả.");
    }
}
