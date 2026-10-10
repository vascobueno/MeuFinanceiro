namespace MeuFinanceiro.Domain.Enums;

/// <summary>"Vencido" não é status: é calculado pela validade, porque acontece sozinho.</summary>
public enum IssuedCardStatus
{
    Ativo = 1,
    /// <summary>Trocado por outro (renovação, perda): <c>ReplacedById</c> aponta o novo.</summary>
    Substituido = 2,
    Cancelado = 3
}
