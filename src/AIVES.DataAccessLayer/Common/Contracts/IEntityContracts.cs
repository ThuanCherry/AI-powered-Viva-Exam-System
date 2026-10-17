namespace AIVES.DataAccessLayer.Common.Contracts;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
}

public interface ISoftDeletable
{
    bool IsActive { get; set; }
}
