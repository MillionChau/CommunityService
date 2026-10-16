using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;

namespace Community.Infrastructure.Repositories;

public class CommentReportRepository : BaseRepository<CommentReport>, ICommentReportRepository
{
    public CommentReportRepository(CommunityDbContext context) : base(context)
    {
    }
}
