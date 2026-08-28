namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>
/// Score de conciliação reutilizado para extrato bancário e fatura de cartão
/// (docs/08-conciliacao-cartoes.md): valor 35, data 25, descrição 25, parcela/documento 10, tipo 5.
/// 90–100 MATCHED; 70–89 POSSIBLE_MATCH; abaixo de 70 UNMATCHED.
/// </summary>
public static class ReconciliationScorer
{
    public const int MatchedThreshold = 90;
    public const int PossibleMatchThreshold = 70;

    public record ScoreResult(int Total, decimal AmountDifference, int DateDifferenceDays, int DescriptionScore);

    public static ScoreResult Score(
        decimal candidateAmount, decimal itemAmount,
        DateOnly candidateDate, DateOnly itemDate,
        string candidateDescription, string itemDescription,
        bool sameDocumentOrInstallment)
    {
        var amountDifference = Math.Abs(candidateAmount - itemAmount);
        var amountScore = amountDifference == 0 ? 35 : amountDifference <= 0.05m ? 30 : amountDifference <= 1m ? 15 : 0;

        var dateDifferenceDays = Math.Abs(candidateDate.DayNumber - itemDate.DayNumber);
        var dateScore = dateDifferenceDays switch
        {
            0 => 25,
            <= 1 => 20,
            <= 3 => 12,
            <= 7 => 5,
            _ => 0
        };

        var descriptionScore = DescriptionSimilarityScore(candidateDescription, itemDescription);

        var documentScore = sameDocumentOrInstallment ? 10 : 0;
        const int typeScore = 5; // reservado para comparação futura de tipo de movimento/origem

        var total = amountScore + dateScore + descriptionScore + documentScore + typeScore;

        return new ScoreResult(total, amountDifference, dateDifferenceDays, descriptionScore);
    }

    public static string StatusFor(int score) => score switch
    {
        >= MatchedThreshold => "MATCHED",
        >= PossibleMatchThreshold => "POSSIBLE_MATCH",
        _ => "UNMATCHED"
    };

    /// <summary>Similaridade simples por prefixo/substring (0-25) — sem dependências externas.</summary>
    private static int DescriptionSimilarityScore(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);

        if (normalizedA == normalizedB) return 25;
        if (normalizedA.Contains(normalizedB) || normalizedB.Contains(normalizedA)) return 18;

        var wordsA = normalizedA.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = normalizedB.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var commonWords = wordsA.Count(w => wordsB.Contains(w));

        if (commonWords == 0) return 0;
        var ratio = (double)commonWords / Math.Max(wordsA.Length, 1);
        return (int)Math.Round(ratio * 15);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
