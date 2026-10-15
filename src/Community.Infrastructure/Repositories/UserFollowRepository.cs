using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class UserFollowRepository : BaseRepository<UserFollow>, IUserFollowRepository
{
    public UserFollowRepository(CommunityDbContext context) : base(context)
    {
    }

    public async Task<bool> IsFollowingAsync(Guid followerId, Guid followeeId, CancellationToken cancellationToken = default)
    {
        return await _context.UserFollows
            .AnyAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId, cancellationToken);
    }

    public async Task<int> GetFollowersCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserFollows
            .CountAsync(f => f.FolloweeId == userId, cancellationToken);
    }

    public async Task<int> GetFollowingCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserFollows
            .CountAsync(f => f.FollowerId == userId, cancellationToken);
    }
}
