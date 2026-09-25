using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class CommentLikeRepository : BaseRepository<CommentLike>, ICommentLikeRepository
{
    public CommentLikeRepository(CommunityDbContext context) : base(context)
    {
    }

    public async Task<CommentLike?> GetByCommentAndUserAsync(Guid commentId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<CommentLike>()
            .FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId, cancellationToken);
    }
}
