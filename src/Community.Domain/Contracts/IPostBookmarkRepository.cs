using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;

public interface IPostBookmarkRepository : IRepository<PostBookmark>
{
    Task<PostBookmark?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);
}
