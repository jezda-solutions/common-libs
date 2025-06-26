using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Data;

public abstract class UnitOfWork(DbContext context) : IDisposable
{
    protected readonly DbContext _context = context;

    /// <summary>
    /// Initializes the repositories for the unit of work.
    /// </summary>
    public abstract void InitializeRepositories();

    public int SaveChanges()
    {
        return _context.SaveChanges();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
    }
}