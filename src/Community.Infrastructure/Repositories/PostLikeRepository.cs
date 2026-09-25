using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class PostLikeRepository : BaseRepository<PostLike>, IPostLikeRepository
{
    public PostLikeRepository(CommunityDbContext context) : base(context)
    {
    }

    public async Task<PostLike?> GetByPostAndUserAsync(Guid postId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<PostLike>()
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId, cancellationToken);
    }
}
