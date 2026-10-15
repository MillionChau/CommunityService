using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Repositories;

public class TechZoneRepository : BaseRepository<TechZone>, ITechZoneRepository
{
    public TechZoneRepository(CommunityDbContext context) : base(context)
    {
    }

    public async Task<TechZone?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLower();
        return await _context.Set<TechZone>()
            .FirstOrDefaultAsync(t => t.Slug.ToLower() == normalizedSlug, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLower();
        var query = _context.Set<TechZone>().Where(t => t.Slug.ToLower() == normalizedSlug);
        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }
}
