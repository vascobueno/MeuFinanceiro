using MeuFinanceiro.Application.Common.Exceptions;

namespace MeuFinanceiro.Application.Accounts;

/// <summary>Banco, agência e conta já normalizados — ou todos nulos.</summary>
public record BankDetails(string? BankCode, string? Agency, string? AgencyDigit, string? AccountNumber, string? AccountDigit)
{
    public static readonly BankDetails None = new(null, null, null, null, null);

    public bool IsEmpty => AccountNumber is null;

    /// <summary>
    /// Normaliza o que o usuário digitou e valida. Os campos são opcionais, mas vêm juntos: uma
    /// conta com número e sem agência não identifica nada — e é pelo conjunto que a importação de
    /// OFX (etapa 5) vai reconhecer a conta. O dígito da agência é o único que pode faltar.
    ///
    /// Pontuação é descartada ("12.345-6" no número vira "123456"); o código do banco e a agência
    /// ganham zeros à esquerda ("33" → "033", "1" → "0001"), como saem no extrato e no OFX.
    /// </summary>
    public static BankDetails Parse(string? bankCode, string? agency, string? agencyDigit, string? accountNumber, string? accountDigit)
    {
        var code = Digits(bankCode);
        var ag = Digits(agency);
        var agDigit = CheckDigit(agencyDigit);
        var number = Digits(accountNumber);
        var digit = CheckDigit(accountDigit);

        // Antes da checagem de completude: um dígito inválido não pode ser lido como "em branco".
        if (!string.IsNullOrWhiteSpace(agencyDigit) && agDigit is null || !string.IsNullOrWhiteSpace(accountDigit) && digit is null)
            throw new BusinessRuleException("digito-invalido", "O dígito é um número de 0 a 9 ou a letra X.");

        if (code is null && ag is null && agDigit is null && number is null && digit is null) return None;

        if (code is null || ag is null || number is null || digit is null)
            throw new BusinessRuleException("dados-bancarios-incompletos",
                "Para guardar os dados bancários, informe banco, agência, conta e dígito da conta — ou deixe todos em branco.");

        if (code.Length > 3)
            throw new BusinessRuleException("banco-invalido", "O código do banco tem 3 dígitos.");
        if (ag.Length > 4)
            throw new BusinessRuleException("agencia-invalida", "A agência tem até 4 dígitos.");
        if (number.Length > 13)
            throw new BusinessRuleException("conta-invalida", "O número da conta tem até 13 dígitos.");

        return new BankDetails(code.PadLeft(3, '0'), ag.PadLeft(4, '0'), agDigit, number, digit);
    }

    /// <summary>"0001-5 / 12345-6", para a tela e mensagens.</summary>
    public string? Display =>
        IsEmpty ? null : $"{Agency}{(AgencyDigit is null ? "" : "-" + AgencyDigit)} / {AccountNumber}-{AccountDigit}";

    private static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            throw new BusinessRuleException("dados-bancarios-invalidos", $"\"{value.Trim()}\" não tem números.");
        return digits;
    }

    /// <summary>Um dígito ou X; nulo se em branco; nulo também se inválido (quem chama distingue).</summary>
    private static string? CheckDigit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim().ToUpperInvariant();
        return s.Length == 1 && (char.IsDigit(s[0]) || s[0] == 'X') ? s : null;
    }
}
