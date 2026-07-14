namespace Community.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> GetRepository<T>() where T : class;
        Task<int> SaveAsync(CancellationToken cancellationToken = default);
    }
}
