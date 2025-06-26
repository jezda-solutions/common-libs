using Jezda.Common.Domain.Entities.Interfaces;
using System;

namespace Jezda.Common.Domain.Entities.Base;

public abstract class AuditableBaseEntity<T> : AuditableBaseEntity
{
    public virtual T Id { get; set; } = default!;
}

public class AuditableBaseEntity : IAuditEntity, IDeletedEntity
{
    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedOnUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ModifiedBy { get; set; }

    public DateTimeOffset? ModifiedOnUtc { get; set; }

    public bool IsDeleted { get; set; } = false;
}