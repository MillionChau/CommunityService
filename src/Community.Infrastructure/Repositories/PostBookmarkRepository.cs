using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class PostBookmarkRepository : BaseRepository<PostBookmark>, IPostBookmarkRepository
{
    public PostBookmarkRepository(CommunityDbContext context) : base(context)
    {
    }

    public async Task<PostBookmark?> GetByPostAndUserAsync(Guid postId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<PostBookmark>()
            .FirstOrDefaultAsync(b => b.PostId == postId && b.UserId == userId, cancellationToken);
    }
}
