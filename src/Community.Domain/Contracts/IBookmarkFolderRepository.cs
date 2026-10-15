using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;

public interface IBookmarkFolderRepository : IRepository<BookmarkFolder>
{
    Task<IEnumerable<BookmarkFolder>> GetUserFoldersAsync(Guid userId, CancellationToken cancellationToken = default);
}
