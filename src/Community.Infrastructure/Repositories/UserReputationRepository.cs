using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;

namespace Community.Infrastructure.Repositories;

public class UserReputationRepository : BaseRepository<UserReputation>, IUserReputationRepository
{
    public UserReputationRepository(CommunityDbContext context) : base(context)
    {
    }
}
