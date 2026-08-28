using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Estabelecimento/descrição usado no autocomplete por prefixo (RF005, regra 18).
/// UsageCount/LastUsedAt priorizam frequência e uso recente (docs/05-regras-negocio.md).
/// </summary>
public class Merchant : HouseholdOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public Guid? DefaultCategoryId { get; set; }
    public Category? DefaultCategory { get; set; }

    public ICollection<MerchantAlias> Aliases { get; set; } = new List<MerchantAlias>();
}
