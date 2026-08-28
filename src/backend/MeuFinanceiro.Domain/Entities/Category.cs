using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

public class Category : HouseholdOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public CategoryType Type { get; set; }
    public bool IsSystemDefault { get; set; }

    public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
}
