using MeuFinanceiro.Domain.Entities;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Qual conta cadastrada é a do OFX, pelos dados bancários (etapa 4).
///
/// Os bancos escrevem o ACCTID de jeitos diferentes: só o número ("12345"), número e dígito
/// ("12345-6", "123456"), ou agência na frente ("0001123456"); o BANKID às vezes com 4 dígitos
/// ("0033"); o BRANCHID às vezes ausente. Por isso a comparação é só com dígitos, sem zeros à
/// esquerda, aceitando essas formas — e o resultado só vale se UMA conta casar.
/// </summary>
public static class OfxAccountMatcher
{
    public static bool Matches(Account account, string? bankId, string? branchId, string? acctId)
    {
        if (account.AccountNumber is null || account.Agency is null || account.BankCode is null) return false;

        var acct = Digits(acctId);
        if (acct.Length == 0) return false;

        var bank = Digits(bankId);
        if (bank.Length > 0 && Trim(bank) != Trim(account.BankCode)) return false;

        var branch = Digits(branchId);
        // A agência pode vir com o dígito colado ("00015").
        if (branch.Length > 0 && Trim(branch) != Trim(account.Agency) && Trim(branch) != Trim(account.Agency + account.AgencyDigit))
            return false;

        var number = account.AccountNumber;
        var digit = account.AccountDigit is { } d && char.IsDigit(d[0]) ? d : "";
        var forms = new[]
        {
            number + digit,
            number,
            account.Agency + number + digit,
            account.Agency + number,
        };
        var target = Trim(acct);
        return forms.Any(f => Trim(f) == target);
    }

    /// <summary>"banco 033, agência 0001, conta 12345-6" — para dizer o que o arquivo traz.</summary>
    public static string Describe(string? bankId, string? branchId, string? acctId)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(bankId)) parts.Add($"banco {bankId.Trim()}");
        if (!string.IsNullOrWhiteSpace(branchId)) parts.Add($"agência {branchId.Trim()}");
        if (!string.IsNullOrWhiteSpace(acctId)) parts.Add($"conta {acctId.Trim()}");
        return parts.Count == 0 ? "sem dados da conta" : string.Join(", ", parts);
    }

    private static string Digits(string? value) => value is null ? "" : new string(value.Where(char.IsDigit).ToArray());

    private static string Trim(string value)
    {
        var t = value.TrimStart('0');
        return t.Length == 0 ? "0" : t;
    }
}
