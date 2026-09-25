using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;

public interface ICommentLikeRepository : IRepository<CommentLike>
{
    Task<CommentLike?> GetByCommentAndUserAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
}
