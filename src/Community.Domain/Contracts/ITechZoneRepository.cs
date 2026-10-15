using Community.Domain.Entities;
using Community.Domain.Interfaces;

namespace Community.Domain.Contracts;

public interface ITechZoneRepository : IRepository<TechZone>
{
    Task<TechZone?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
