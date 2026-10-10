using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Domain.Entities;

namespace MeuFinanceiro.Application.Transactions;

/// <summary>
/// Dados de compra no exterior do lançamento (docs/06): validação e cópia a partir do item da
/// fatura na conciliação. Parcelamento e recorrência não levam: o valor estrangeiro é da compra,
/// não de cada parcela.
/// </summary>
public static class ForeignPurchase
{
    public static void Apply(FinancialTransaction t, ForeignPurchaseRequest? f)
    {
        if (f is null)
        {
            t.ForeignAmount = null; t.ForeignCurrency = null; t.ExchangeRate = null;
            t.OriginalAmount = null; t.OriginalCurrency = null;
            return;
        }

        var currency = Currency(f.Currency, "moeda-invalida");
        if (f.Amount <= 0)
            throw new BusinessRuleException("valor-estrangeiro-invalido", "Informe o valor na moeda estrangeira, maior que zero.");
        if (f.ExchangeRate is <= 0)
            throw new BusinessRuleException("cotacao-invalida", "A cotação tem de ser maior que zero.");
        if ((f.OriginalAmount is null) != string.IsNullOrWhiteSpace(f.OriginalCurrency))
            throw new BusinessRuleException("moeda-de-origem-incompleta", "Informe valor e moeda de origem — ou nenhum dos dois.");

        t.ForeignAmount = f.Amount;
        t.ForeignCurrency = currency;
        t.ExchangeRate = f.ExchangeRate;
        t.OriginalAmount = f.OriginalAmount;
        t.OriginalCurrency = f.OriginalAmount is null ? null : Currency(f.OriginalCurrency, "moeda-de-origem-invalida");
    }

    public static FinancialTransaction WithForeign(this FinancialTransaction t, ForeignPurchaseRequest? f)
    {
        if (f is not null) Apply(t, f);
        return t;
    }

    /// <summary>
    /// Na conciliação com o item da fatura: o lançamento que não tinha dados de câmbio fica com os
    /// da fatura, que são os que o banco usou. Os que o usuário já informou não são trocados.
    /// </summary>
    public static void CopyFromInvoiceItem(FinancialTransaction t, CardInvoiceItem item)
    {
        // O cartão da seção da fatura também vale para o lançamento que não o tinha.
        if (t.IssuedCardId is null && item.IssuedCardId is not null) t.IssuedCardId = item.IssuedCardId;

        if (t.ForeignAmount is not null || item.ForeignAmount is null) return;
        t.ForeignAmount = Math.Abs(item.ForeignAmount.Value);
        t.ForeignCurrency = item.ForeignCurrency;
        t.ExchangeRate = item.ExchangeRate;
        t.OriginalAmount = item.OriginalAmount is { } o ? Math.Abs(o) : null;
        t.OriginalCurrency = item.OriginalCurrency;
    }

    public static ForeignPurchaseResponse? ToResponse(FinancialTransaction t) =>
        t.ForeignAmount is { } amount && t.ForeignCurrency is { } currency
            ? new ForeignPurchaseResponse(amount, currency, t.ExchangeRate, t.OriginalAmount, t.OriginalCurrency)
            : null;

    private static string Currency(string? raw, string rule)
    {
        var c = raw?.Trim().ToUpperInvariant() ?? "";
        if (c.Length != 3 || !c.All(char.IsLetter) || c == "BRL")
            throw new BusinessRuleException(rule, "Moeda é o código de 3 letras (USD, EUR, GBP…), diferente de BRL.");
        return c;
    }
}
