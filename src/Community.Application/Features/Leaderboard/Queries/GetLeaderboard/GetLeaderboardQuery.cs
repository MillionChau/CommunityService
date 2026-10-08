using Community.Application.Common.Models;
using Community.Application.DTOs.Leaderboard;
using Community.Domain.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Leaderboard.Queries.GetLeaderboard;

public record GetLeaderboardQuery : IRequest<ResponseModel<List<LeaderboardUserDto>>>
{
    public int Top { get; init; } = 10;
    public string Period { get; init; } = "all"; // all, weekly, monthly
}

public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, ResponseModel<List<LeaderboardUserDto>>>
{
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;

    public GetLeaderboardQueryHandler(
        IPostRepository postRepository,
        ICommentRepository commentRepository)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
    }

    public async Task<ResponseModel<List<LeaderboardUserDto>>> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var top = request.Top <= 0 || request.Top > 100 ? 10 : request.Top;

        // Group posts by AuthorId to calculate activity points
        var postGroups = await _postRepository.GetByExpression(p => p.AuthorId != null && p.IsDeleted != 1)
            .GroupBy(p => p.AuthorId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                PostCount = g.Count(),
                LikesReceived = g.Sum(p => p.LikesCount ?? 0)
            })
            .ToListAsync(cancellationToken);

        // Group comments by CreatedBy (Guid?)
        var commentGroups = await _commentRepository.GetByExpression(c => c.CreatedBy != null && c.IsDeleted != 1)
            .GroupBy(c => c.CreatedBy!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                CommentCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var commentDict = commentGroups.ToDictionary(cg => cg.UserId, cg => cg.CommentCount);

        var allUserIds = postGroups.Select(p => p.UserId).Union(commentDict.Keys).Distinct();

        var leaderboard = new List<LeaderboardUserDto>();

        foreach (var userId in allUserIds)
        {
            var pInfo = postGroups.FirstOrDefault(p => p.UserId == userId);
            var postCount = pInfo?.PostCount ?? 0;
            var likes = pInfo?.LikesReceived ?? 0;
            var comments = commentDict.TryGetValue(userId, out var cc) ? cc : 0;

            // Score formula: 10 points per post + 5 points per like received + 2 points per comment
            var score = (postCount * 10) + (likes * 5) + (comments * 2);

            var badges = new List<string>();
            if (score >= 500) badges.Add("Tech Lead");
            else if (score >= 200) badges.Add("Top Contributor");
            else if (score >= 50) badges.Add("Active Member");

            leaderboard.Add(new LeaderboardUserDto
            {
                UserId = userId,
                Username = $"Dev_{userId.ToString().Substring(0, 6)}",
                Score = score,
                PostCount = postCount,
                LikeCount = likes,
                CommentCount = comments,
                Badges = badges
            });
        }

        var sorted = leaderboard
            .OrderByDescending(x => x.Score)
            .Take(top)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return ResponseModel<List<LeaderboardUserDto>>.Success(sorted, "Leaderboard retrieved successfully.");
    }
}
