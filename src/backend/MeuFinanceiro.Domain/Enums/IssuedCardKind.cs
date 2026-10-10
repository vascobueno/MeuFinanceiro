namespace MeuFinanceiro.Domain.Enums;

/// <summary>Os cartões de um mesmo grupo (fatura): o do titular, os adicionais e os virtuais.</summary>
public enum IssuedCardKind
{
    Titular = 1,
    Adicional = 2,
    /// <summary>Cartão virtual, para compra online — na fatura costuma vir marcado (o "@" do Santander).</summary>
    Online = 3
}
