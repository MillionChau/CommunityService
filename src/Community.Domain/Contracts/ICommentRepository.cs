using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;
public interface ICommentRepository : IRepository<Comment>
{
    Task<IEnumerable<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
}