using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Cards;

/// <summary>Bandeira: leitura do que vem da tela e sugestão pelos primeiros dígitos do cartão.</summary>
public static class CardBrands
{
    private static readonly Dictionary<string, CardBrand> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["master"] = CardBrand.Mastercard,
        ["master card"] = CardBrand.Mastercard,
        ["amex"] = CardBrand.AmericanExpress,
        ["american express"] = CardBrand.AmericanExpress,
        ["hiper"] = CardBrand.Hipercard,
        ["diners"] = CardBrand.DinersClub,
        ["diners club"] = CardBrand.DinersClub,
        ["union pay"] = CardBrand.UnionPay,
        ["visa electron"] = CardBrand.VisaElectron,
        ["elo debito"] = CardBrand.EloDebito,
        ["elo débito"] = CardBrand.EloDebito,
    };

    public static bool IsDebitOnly(CardBrand brand) =>
        brand is CardBrand.Maestro or CardBrand.VisaElectron or CardBrand.EloDebito;

    /// <summary>
    /// Bandeira de um grupo de cartões de crédito. Aceita o nome do enum e os apelidos de sempre
    /// ("Master", "Amex") — clientes antigos mandavam texto livre. Em branco é "não informada".
    /// </summary>
    public static CardBrand? ParseForCreditGroup(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();

        if (!Aliases.TryGetValue(value, out var brand) &&
            !(Enum.TryParse(value.Replace(" ", ""), ignoreCase: true, out brand) && Enum.IsDefined(brand) && !int.TryParse(value, out _)))
        {
            throw new BusinessRuleException("bandeira-invalida",
                $"Bandeira \"{value}\" não reconhecida. Escolha uma da lista ou \"Outra\".");
        }

        if (IsDebitOnly(brand))
            throw new BusinessRuleException("bandeira-de-debito",
                "Maestro, Visa Electron e Elo Débito são bandeiras só de débito — não de um cartão de crédito.");
        return brand;
    }

    /// <summary>Qualquer bandeira, inclusive as de débito — para o cartão só de débito.</summary>
    public static CardBrand? ParseAny(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        if (Aliases.TryGetValue(value, out var alias)) return alias;
        if (Enum.TryParse(value.Replace(" ", ""), ignoreCase: true, out CardBrand brand) && Enum.IsDefined(brand) && !int.TryParse(value, out _))
            return brand;
        throw new BusinessRuleException("bandeira-invalida",
            $"Bandeira \"{value}\" não reconhecida. Escolha uma da lista ou \"Outra\".");
    }

    /// <summary>
    /// A versão de débito pertence à família da de crédito: dígitos de Visa num Visa Electron, ou
    /// de Mastercard num Maestro, não são divergência.
    /// </summary>
    public static bool SameFamily(CardBrand a, CardBrand b) => Family(a) == Family(b);

    private static CardBrand Family(CardBrand brand) => brand switch
    {
        CardBrand.VisaElectron => CardBrand.Visa,
        CardBrand.Maestro => CardBrand.Mastercard,
        CardBrand.EloDebito => CardBrand.Elo,
        _ => brand,
    };

    /// <summary>
    /// Cartão de débito a sugerir para uma compra no débito de bandeira conhecida ("COMPRA CARTAO
    /// DEB MC" é Mastercard): só quando há exatamente UM cartão ativo da família dessa bandeira com
    /// débito na conta. Com dois, sugerir um deles seria chute.
    /// </summary>
    public static T? PickDebitCard<T>(IEnumerable<T> activeDebitCards, Func<T, CardBrand?> brandOf, CardBrand purchaseBrand)
        where T : class
    {
        var same = activeDebitCards.Where(c => brandOf(c) is { } b && SameFamily(b, purchaseBrand)).Take(2).ToList();
        return same.Count == 1 ? same[0] : null;
    }

    private sealed record Range(string Start, string End, CardBrand Brand)
    {
        public int Length => Start.Length;
    }

    // Faixas de BIN publicadas pelas bandeiras. As mais específicas (6 dígitos) convivem com as
    // gerais: 401178 é Elo dentro do "4" da Visa, 3841xx é Hipercard dentro do "38" do Diners.
    // Cabal não entra: as faixas dela no Brasil não são públicas o bastante para sugerir.
    private static readonly Range[] Ranges = BuildRanges();

    private static Range[] BuildRanges()
    {
        var r = new List<Range>
        {
            new("4", "4", CardBrand.Visa),
            new("51", "55", CardBrand.Mastercard),
            new("2221", "2720", CardBrand.Mastercard),
            new("34", "34", CardBrand.AmericanExpress),
            new("37", "37", CardBrand.AmericanExpress),
            new("300", "305", CardBrand.DinersClub),
            new("309", "309", CardBrand.DinersClub),
            new("36", "36", CardBrand.DinersClub),
            new("38", "39", CardBrand.DinersClub),
            new("3528", "3589", CardBrand.Jcb),
            new("6011", "6011", CardBrand.Discover),
            new("644", "649", CardBrand.Discover),
            new("65", "65", CardBrand.Discover),
            new("62", "62", CardBrand.UnionPay),
            new("606282", "606282", CardBrand.Hipercard),
            new("384100", "384100", CardBrand.Hipercard),
            new("384140", "384140", CardBrand.Hipercard),
            new("384160", "384160", CardBrand.Hipercard),
        };
        foreach (var (start, end) in new[]
                 {
                     ("401178", "401179"), ("431274", "431274"), ("438935", "438935"), ("451416", "451416"),
                     ("457393", "457393"), ("457631", "457632"), ("504175", "504175"), ("506699", "506778"),
                     ("509000", "509999"), ("627780", "627780"), ("636297", "636297"), ("636368", "636368"),
                     ("650031", "650033"), ("650035", "650051"), ("650405", "650439"), ("650485", "650538"),
                     ("650541", "650598"), ("650700", "650718"), ("650720", "650727"), ("650901", "650978"),
                     ("651652", "651679"), ("655000", "655019"), ("655021", "655058"),
                 })
        {
            r.Add(new Range(start, end, CardBrand.Elo));
        }
        return r.ToArray();
    }

    /// <summary>
    /// Bandeira pelos primeiros dígitos — só quando não há dúvida. Com 4 dígitos, "4011" pode ser
    /// Visa ou Elo (401178), então não sugere nada; com "401178", é Elo. Uma sugestão errada seria
    /// pior que nenhuma: ela vira aviso de divergência na tela.
    /// </summary>
    public static CardBrand? SuggestFromDigits(string? firstDigits)
    {
        if (string.IsNullOrWhiteSpace(firstDigits) || !firstDigits.All(char.IsDigit)) return null;
        var d = firstDigits;

        // Faixas que os dígitos já decidem (a faixa cabe neles), da mais específica para a geral.
        var decided = Ranges
            .Where(r => r.Length <= d.Length && string.CompareOrdinal(d[..r.Length], r.Start) >= 0 && string.CompareOrdinal(d[..r.Length], r.End) <= 0)
            .ToList();
        if (decided.Count == 0) return null;

        var longest = decided.Max(r => r.Length);
        var best = decided.Where(r => r.Length == longest).Select(r => r.Brand).Distinct().ToList();
        if (best.Count != 1) return null;

        // Faixas mais longas que os dígitos e que ainda podem conter o cartão: se forem de outra
        // bandeira, a dúvida fica.
        var stillOpen = Ranges.Any(r => r.Length > d.Length && r.Brand != best[0]
            && string.CompareOrdinal(d, r.Start[..d.Length]) >= 0 && string.CompareOrdinal(d, r.End[..d.Length]) <= 0);

        return stillOpen ? null : best[0];
    }
}
