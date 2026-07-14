using System.Linq.Expressions;

namespace Community.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
        IQueryable<T> GetByExpression(Expression<Func<T, bool>>? expression = null, CancellationToken cancellationToken = default);

        Task AddAsync(T entity, CancellationToken cancellationToken = default);
        Task AddRangeAsync(List<T> entity, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<T> entity, CancellationToken cancellationToken = default);

        Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
        Task UpdateRange(List<T> entity, CancellationToken cancellationToken = default);
        Task UpdateRange(IEnumerable<T> entity, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task DeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
        Task DeleteRange(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task DeleteByExpresstion(Expression<Func<T, bool>>? expression, CancellationToken cancellationToken = default);
        Task DeleteRange(List<T> entities, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    }
}
