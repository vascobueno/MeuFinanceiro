using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

public class Subcategory : AuditableEntity
{
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Name { get; set; } = string.Empty;
}
