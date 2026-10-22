using Community.Application.Common.Models;
using Community.Application.DTOs.Leaderboard;
using Community.Application.Interfaces;
using MediatR;

namespace Community.Application.Features.Leaderboard.Queries.GetLeaderboard;

public record GetLeaderboardQuery : IRequest<ResponseModel<List<LeaderboardUserDto>>>
{
    public int Top { get; init; } = 10;
    public string Period { get; init; } = "all"; // all, weekly, monthly
}

public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, ResponseModel<List<LeaderboardUserDto>>>
{
    private readonly IReputationService _reputationService;

    public GetLeaderboardQueryHandler(IReputationService reputationService)
    {
        _reputationService = reputationService;
    }

    public async Task<ResponseModel<List<LeaderboardUserDto>>> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var top = request.Top <= 0 || request.Top > 100 ? 10 : request.Top;
        var period = string.IsNullOrWhiteSpace(request.Period) ? "all" : request.Period.ToLowerInvariant();

        var leaderboard = await _reputationService.GetLeaderboardAsync(period, top, cancellationToken);
        return ResponseModel<List<LeaderboardUserDto>>.Success(leaderboard, "Leaderboard retrieved successfully.");
    }
}
