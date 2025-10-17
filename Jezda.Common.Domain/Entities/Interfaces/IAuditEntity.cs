using System;

namespace Jezda.Common.Domain.Entities.Interfaces;

public interface IAuditEntity
{
    Guid CreatedBy { get; set; }
    
    DateTimeOffset CreatedOn { get; set; }
    
    Guid? UpdatedBy { get; set; }
    
    DateTimeOffset? UpdatedOn { get; set; }
    
    bool IsDeleted { get; set; }
}
