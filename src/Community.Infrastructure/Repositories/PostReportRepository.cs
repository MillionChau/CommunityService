using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class PostReportRepository : BaseRepository<PostReport>, IPostReportRepository
{
    public PostReportRepository(CommunityDbContext context) : base(context) { }

    public async Task<IEnumerable<PostReport>> GetReportsByPostIdAsync(Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PostReports
            .Where(r => r.PostId == postId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync(cancellationToken);
    }
}
