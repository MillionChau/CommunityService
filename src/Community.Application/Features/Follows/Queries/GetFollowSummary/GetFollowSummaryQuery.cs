using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using MediatR;

namespace Community.Application.Features.Follows.Queries.GetFollowSummary;

public record GetFollowSummaryQuery(Guid UserId) : IRequest<ResponseModel<UserFollowSummaryDto>>;

public class GetFollowSummaryQueryHandler
    : IRequestHandler<GetFollowSummaryQuery, ResponseModel<UserFollowSummaryDto>>
{
    private readonly IUserFollowRepository _userFollowRepository;
    private readonly ICurrentUserService _currentUser;

    public GetFollowSummaryQueryHandler(
        IUserFollowRepository userFollowRepository,
        ICurrentUserService currentUser)
    {
        _userFollowRepository = userFollowRepository;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<UserFollowSummaryDto>> Handle(
        GetFollowSummaryQuery request, CancellationToken cancellationToken)
    {
        var followersCount = await _userFollowRepository.GetFollowersCountAsync(request.UserId, cancellationToken);
        var followingCount = await _userFollowRepository.GetFollowingCountAsync(request.UserId, cancellationToken);

        bool isFollowing = false;
        if (_currentUser.IsAuthenticated && _currentUser.UserId is Guid currentUserId && currentUserId != request.UserId)
        {
            isFollowing = await _userFollowRepository.IsFollowingAsync(currentUserId, request.UserId, cancellationToken);
        }

        var summary = new UserFollowSummaryDto
        {
            UserId = request.UserId,
            FollowersCount = followersCount,
            FollowingCount = followingCount,
            IsFollowing = isFollowing
        };

        return ResponseModel<UserFollowSummaryDto>.Success(summary);
    }
}
