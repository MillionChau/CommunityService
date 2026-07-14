using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;

namespace Community.Infrastructure.Repositories;

public class PostRepository : BaseRepository<Post>, IPostRepository
{
    public PostRepository(CommunityDbContext context) : base(context)
    {
    }
}
