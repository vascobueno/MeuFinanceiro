using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Nomes alternativos do mesmo estabelecimento (útil para conciliação de fatura/extrato).</summary>
public class MerchantAlias : AuditableEntity
{
    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public string Alias { get; set; } = string.Empty;
}
