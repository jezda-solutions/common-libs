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

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public bool IsDeleted { get; set; } = false;
}