using System.Globalization;
using System.Text;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Tipo do movimento do extrato a partir da descrição e do sinal (docs/09). Antes todo movimento
/// colado virava OTHER_EXPENSE ou OTHER_INCOME.
///
/// As regras são avaliadas em ordem e a primeira que casa vence — a ordem importa: "PAGAMENTO
/// CARTAO DE CREDITO" tem de virar BILL_PAYMENT antes que "CARTAO" o leve para compra no débito, e
/// "RESGATE" antes de "APLICACAO" quando a linha traz os dois ("RESGATE APLICACAO CDB").
/// O sinal separa os pares: PIX e TED enviados ou recebidos, aplicação ou resgate.
/// </summary>
public static class StatementItemClassifier
{
    public const string DebitCardPurchase = "DEBIT_CARD_PURCHASE";

    /// <summary>Todos os tipos conhecidos — o que a importação aceita no campo tipo.</summary>
    public static readonly IReadOnlySet<string> KnownTypes = new HashSet<string>
    {
        DebitCardPurchase, "ATM_WITHDRAWAL", "PIX_SENT", "PIX_RECEIVED", "TED_SENT", "TED_RECEIVED",
        "BOLETO_PAYMENT", "BILL_PAYMENT", "TAX_PAYMENT", "DIRECT_DEBIT", "BANK_FEE", "INTEREST_CHARGE",
        "INVESTMENT", "INVESTMENT_REDEMPTION", "INVESTMENT_INTEREST", "TRANSFER", "OTHER_INCOME", "OTHER_EXPENSE",
    };

    private sealed record Rule(string[] Any, string Type, int Sign = 0);

    // Sign: -1 só débito, +1 só crédito, 0 qualquer um.
    private static readonly Rule[] Rules =
    [
        // Fatura de cartão paga pela conta — antes de "CARTAO" e de "PAGAMENTO".
        new(["PAGAMENTO CARTAO DE CREDITO", "PAGAMENTO CARTAO CREDITO", "PAGTO CARTAO", "PAG FATURA", "PAGTO FATURA", "PAGAMENTO FATURA", "PAGAMENTO DE FATURA"], "BILL_PAYMENT", -1),
        new(["COMPRA CARTAO", "COMPRA DEBITO", "COMPRA COM CARTAO", "COMPRA NO DEBITO", "CARTAO DEB", "COMPRA ELO DEB", "COMPRA VISA ELECTRON", "COMPRA MAESTRO"], DebitCardPurchase, -1),
        new(["SAQUE"], "ATM_WITHDRAWAL", -1),
        new(["PIX"], "PIX_SENT", -1),
        new(["PIX"], "PIX_RECEIVED", 1),
        new(["TED", "DOC "], "TED_SENT", -1),
        new(["TED", "DOC "], "TED_RECEIVED", 1),
        new(["BOLETO", "TITULO"], "BOLETO_PAYMENT", -1),
        new(["DARF", "GPS", "DAS ", "IPVA", "IPTU", "TRIBUTO", "IMPOSTO", "IOF"], "TAX_PAYMENT", -1),
        new(["DEBITO AUTOMATICO", "DEB AUTOMATICO", "DEB AUT", "DEBITO AUT"], "DIRECT_DEBIT", -1),
        new(["TARIFA", "CESTA", "PACOTE SERVICOS", "MENSALIDADE PACOTE", "ANUIDADE"], "BANK_FEE", -1),
        new(["JUROS", "ENCARGOS", "MORA", "LIMITE CHEQUE ESPECIAL", "CHEQUE ESPECIAL"], "INTEREST_CHARGE", -1),
        new(["REMUNERACAO", "RENDIMENTO", "RENDIMENTOS", "JUROS SOBRE"], "INVESTMENT_INTEREST", 1),
        new(["RESGATE"], "INVESTMENT_REDEMPTION", 1),
        new(["APLICACAO", "APLIC ", "INVESTIMENTO", "CDB", "RDB", "LCI", "LCA", "TESOURO", "POUPANCA"], "INVESTMENT", -1),
        new(["APLICACAO", "CDB", "RDB", "POUPANCA"], "INVESTMENT_REDEMPTION", 1),
        new(["TRANSF", "TRANSFERENCIA"], "TRANSFER", 0),
    ];

    public static string Classify(string? description, decimal amount)
    {
        var text = $" {Normalize(description)} ";
        var sign = Math.Sign(amount);

        foreach (var rule in Rules)
        {
            if (rule.Sign != 0 && rule.Sign != sign) continue;
            if (rule.Any.Any(word => ContainsWord(text, word))) return rule.Type;
        }
        return amount < 0 ? "OTHER_EXPENSE" : "OTHER_INCOME";
    }

    /// <summary>Tipo informado na colagem estruturada, se conhecido; senão, classificado.</summary>
    public static string Resolve(string? informed, string? description, decimal amount)
    {
        var upper = informed?.Trim().ToUpperInvariant();
        return upper is not null && KnownTypes.Contains(upper) ? upper : Classify(description, amount);
    }

    // Palavra inteira: "TED" não pode casar dentro de "CREDITED" nem "PIX" dentro de outra palavra.
    // As chaves que terminam em espaço ("DOC ", "DAS ") já trazem o limite.
    private static bool ContainsWord(string text, string word)
    {
        var key = word.TrimEnd();
        var index = text.IndexOf(key, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 ? ' ' : text[index - 1];
            var afterIndex = index + key.Length;
            var after = afterIndex >= text.Length ? ' ' : text[afterIndex];
            if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after)) return true;
            index = text.IndexOf(key, index + 1, StringComparison.Ordinal);
        }
        return false;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
