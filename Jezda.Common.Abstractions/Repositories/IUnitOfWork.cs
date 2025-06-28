using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Abstractions.Repositories;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    int SaveChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    IDbContextTransaction BeginTransaction();
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    void CommitTransaction();
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    void RollbackTransaction();
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    IGenericRepository<T> Repository<T>() where T : class;

    void DetachAllEntities();
    bool HasChanges();
}
