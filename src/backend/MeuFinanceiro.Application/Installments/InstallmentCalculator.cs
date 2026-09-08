namespace MeuFinanceiro.Application.Installments;

/// <summary>
/// Regras 15/16/17 (.github/copilot-instructions.md, docs/07-recorrencias-e-parcelamentos.md):
/// parcelamento aceita valor total + quantidade OU valor da parcela + quantidade; a soma
/// das parcelas deve fechar exatamente o principal; o arredondamento fica na última parcela.
/// </summary>
public static class InstallmentCalculator
{
    /// <summary>Modo A: divide um valor total em N parcelas. Ex.: 1000/3 =&gt; 333,33 + 333,33 + 333,34.</summary>
    public static List<decimal> SplitTotal(decimal total, int installmentCount)
    {
        if (installmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "A quantidade de parcelas deve ser maior que zero.");

        var baseAmount = Math.Round(total / installmentCount, 2, MidpointRounding.ToZero);
        var amounts = Enumerable.Repeat(baseAmount, installmentCount).ToList();
        var difference = total - amounts.Sum();
        amounts[^1] += difference; // arredondamento fica na última parcela (regra 17)
        return amounts;
    }

    /// <summary>Modo B: valor fixo por parcela x quantidade. Ex.: 250 x 12 = 3000.</summary>
    public static List<decimal> FromInstallmentAmount(decimal installmentAmount, int installmentCount)
    {
        if (installmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "A quantidade de parcelas deve ser maior que zero.");

        return Enumerable.Repeat(installmentAmount, installmentCount).ToList();
    }
}
