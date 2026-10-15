using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class BookmarkFolderRepository : BaseRepository<BookmarkFolder>, IBookmarkFolderRepository
{
    public BookmarkFolderRepository(CommunityDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<BookmarkFolder>> GetUserFoldersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<BookmarkFolder>()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }
}
