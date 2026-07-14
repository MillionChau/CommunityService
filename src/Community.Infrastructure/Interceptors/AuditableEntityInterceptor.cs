using Community.Application.Interfaces;
using Community.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace Community.Infrastructure.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    public AuditableEntityInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context == null) return;
        var userId = _currentUserService.UserId;
        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedDate = now;
                    entry.Entity.ModifiedBy = null;
                    entry.Entity.ModifiedDate = null;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedBy = userId;
                    entry.Entity.ModifiedDate = now;
                    SafeMarkUnmodified(entry, nameof(IAuditableEntity.CreatedBy));
                    SafeMarkUnmodified(entry, nameof(IAuditableEntity.CreatedDate));
                    break;
            }
        }
    }
    private static void SafeMarkUnmodified(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string propertyName)
    {
        if (entry.Properties.Any(p => p.Metadata.Name == propertyName))
            entry.Property(propertyName).IsModified = false;
    }
}