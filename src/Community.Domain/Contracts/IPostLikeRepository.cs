using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;

public interface IPostLikeRepository : IRepository<PostLike>
{
    Task<PostLike?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);
}
