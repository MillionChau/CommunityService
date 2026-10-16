using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;

public interface IUserFollowRepository : IRepository<UserFollow>
{
    Task<bool> IsFollowingAsync(Guid followerId, Guid followeeId, CancellationToken cancellationToken = default);
    Task<int> GetFollowersCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetFollowingCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
