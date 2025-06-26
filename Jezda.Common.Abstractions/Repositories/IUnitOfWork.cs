using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Abstractions.Repositories;

public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : class;
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void Rollback();
}

