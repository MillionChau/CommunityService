using System.Linq.Expressions;
using Community.Domain.Interfaces;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Base
{
    public class BaseRepository<T> : IRepository<T> where T : class
    {
        protected readonly CommunityDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public BaseRepository(CommunityDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
        }

        public virtual IQueryable<T> GetByExpression(Expression<Func<T, bool>>? expression = null,
            CancellationToken cancellationToken = default)
        {
            return expression == null ? _dbSet.AsQueryable() : _dbSet.Where(expression);
        }

        public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
        }

        public virtual async Task AddRangeAsync(List<T> entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddRangeAsync(entity, cancellationToken);
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddRangeAsync(entity, cancellationToken);
        }

        public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual Task UpdateRange(List<T> entity, CancellationToken cancellationToken = default)
        {
            _dbSet.UpdateRange(entity);
            return Task.CompletedTask;
        }

        public virtual Task UpdateRange(IEnumerable<T> entity, CancellationToken cancellationToken = default)
        {
            _dbSet.UpdateRange(entity);
            return Task.CompletedTask;
        }

        public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity != null)
            {
                _dbSet.Remove(entity);
            }
        }

        public virtual async Task DeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            foreach (var id in ids)
            {
                await DeleteAsync(id, cancellationToken);
            }
        }

        public virtual Task DeleteRange(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        public virtual async Task DeleteByExpresstion(Expression<Func<T, bool>>? expression,
            CancellationToken cancellationToken = default)
        {
            var entities = await GetByExpression(expression).ToListAsync(cancellationToken);
            _dbSet.RemoveRange(entities);
        }

        public virtual Task DeleteRange(List<T> entities, CancellationToken cancellationToken = default)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        public virtual Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return DeleteAsync(id, cancellationToken);
        }

        public virtual Task SoftDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            return DeleteAsync(ids, cancellationToken);
        }
    }
}