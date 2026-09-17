using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Common;

/// <summary>
/// Regra 3: toda movimentação pertence a uma conta OU a um cartão, nunca aos dois. Vale tanto
/// para o lançamento avulso quanto para o cadastro da recorrência que vai gerá-lo, por isso
/// mora aqui em vez de ficar privado em um dos dois services.
/// </summary>
public static class TransactionOriginRules
{
    public static void Validate(Guid? accountId, Guid? creditCardId, TransactionType type)
    {
        if (type == TransactionType.Transfer) return; // transferência valida as duas pontas à parte

        if (accountId is null && creditCardId is null)
            throw new BusinessRuleException("origem-obrigatoria", "Informe uma conta ou um cartão de origem para o lançamento.");

        if (accountId is not null && creditCardId is not null)
            throw new BusinessRuleException("origem-ambigua", "Um lançamento pertence a uma conta OU a um cartão, nunca aos dois (regra 3).");
    }
}
