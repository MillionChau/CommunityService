using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Persistence;
using Community.Infrastructure.Base;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;


public class CommentRepository : BaseRepository<Comment>, ICommentRepository
{
    public CommentRepository(CommunityDbContext context) : base(context) { }

    public async Task<IEnumerable<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await _context.Comments
            .Where(c => c.PostId == postId)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }
}