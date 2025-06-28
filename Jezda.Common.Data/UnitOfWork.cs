using Jezda.Common.Abstractions.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Data;

public abstract class UnitOfWork : IUnitOfWork
{
    protected readonly DbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed = false;
    private readonly Dictionary<Type, Type> _repositories = new();

    protected UnitOfWork(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // Abstract methods for microservices to implement
    protected abstract void ConfigureRepositories();
    protected abstract IGenericRepository<T> CreateGenericRepository<T>() where T : class;

    // Helper method for microservices to register repositories
    protected void RegisterRepository<TEntity, TRepository>()
        where TEntity : class
        where TRepository : class, IGenericRepository<TEntity>
    {
        _repositories[typeof(TEntity)] = typeof(TRepository);
    }

    // Generic repository access
    public virtual IGenericRepository<T> Repository<T>() where T : class
    {
        var entityType = typeof(T);

        if (_repositories.TryGetValue(entityType, out var repositoryType))
        {
            return (IGenericRepository<T>)Activator.CreateInstance(repositoryType, _context)!;
        }

        return CreateGenericRepository<T>();
    }

    // Save methods
    public virtual int SaveChanges()
    {
        try
        {
            return _context.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            throw new DataAccessException("Error occurred while saving changes", ex);
        }
    }

    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new DataAccessException("Error occurred while saving changes", ex);
        }
    }

    // Transaction support
    public virtual IDbContextTransaction BeginTransaction()
    {
        _transaction = _context.Database.BeginTransaction();
        return _transaction;
    }

    public virtual async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _transaction;
    }

    public virtual void CommitTransaction()
    {
        try
        {
            _transaction?.Commit();
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction != null)
                await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public virtual void RollbackTransaction()
    {
        try
        {
            _transaction?.Rollback();
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction != null)
                await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    // Utility methods
    public virtual void DetachAllEntities()
    {
        var entries = _context.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Detached)
            .ToList();

        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    public virtual bool HasChanges()
    {
        return _context.ChangeTracker.HasChanges();
    }

    // Disposal
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context?.Dispose();
        }
        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (_transaction != null)
                await _transaction.DisposeAsync();

            if (_context != null)
                await _context.DisposeAsync();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        Dispose(false);
        GC.SuppressFinalize(this);
    }
}

// Exception class
public class DataAccessException : Exception
{
    public DataAccessException(string message) : base(message) { }
    public DataAccessException(string message, Exception innerException) : base(message, innerException) { }
}