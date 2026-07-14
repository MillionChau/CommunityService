using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Community.Infrastructure.Base
{
    public interface IUnitOfWork : Community.Domain.Interfaces.IUnitOfWork, IAsyncDisposable
    {
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        DbContext GetDbContext();
    }
}