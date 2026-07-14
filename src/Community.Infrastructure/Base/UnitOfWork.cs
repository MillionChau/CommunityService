using System.Collections;
using Community.Domain.Common;
using Community.Domain.Interfaces;
using Community.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Community.Infrastructure.Base;
public class UnitOfWork : IUnitOfWork
{
    private readonly CommunityDbContext _dbContext;
    private readonly IMediator _mediator;
    private Hashtable _repositories = new();
    public UnitOfWork(CommunityDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }
    public IRepository<T> GetRepository<T>() where T : class
    {
        if (_repositories == null) 
            _repositories = new Hashtable();
        var repoType = typeof(T).Name;
        if (!_repositories.ContainsKey(repoType))
        {
            var repositoryInstance = new BaseRepository<T>(_dbContext);
            _repositories.Add(repoType, repositoryInstance);
        }
        return (IRepository<T>)_repositories[repoType]!;
    }
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
    public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        var aggregateRootEntries = _dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
        var domainEvents = aggregateRootEntries
            .SelectMany(e => e.DomainEvents)
            .ToList();
        aggregateRootEntries.ForEach(e => e.ClearDomainEvents());
        var result = await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var domainEvent in domainEvents)
            await _mediator.Publish(domainEvent, cancellationToken);
        return result;
    }
    public DbContext GetDbContext()
    {
        return _dbContext;
    }
    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }
}