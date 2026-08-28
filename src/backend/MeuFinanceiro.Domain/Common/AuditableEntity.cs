namespace MeuFinanceiro.Domain.Common;

/// <summary>
/// Base para entidades com trilha de criação/atualização e concorrência otimista.
/// Ver docs/15-database.md e docs/17-auditoria.md.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Concorrência otimista (docs/15-database.md).</summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>Entidades "de propriedade" de um household (ownership - docs/12-seguranca.md, regra 22).</summary>
public abstract class HouseholdOwnedEntity : AuditableEntity
{
    public Guid HouseholdId { get; set; }
}
