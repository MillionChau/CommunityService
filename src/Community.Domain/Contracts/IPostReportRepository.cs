using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;
public interface IPostReportRepository : IRepository<PostReport>
{
    Task<IEnumerable<PostReport>> GetReportsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
}