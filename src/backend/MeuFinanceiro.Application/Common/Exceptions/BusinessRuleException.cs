namespace MeuFinanceiro.Application.Common.Exceptions;

/// <summary>
/// Violação de regra de negócio (ex.: soma de parcelas, saldo, duplicidade) — mapeada
/// para HTTP 400 com ProblemDetails (docs/13-api.md).
/// </summary>
public class BusinessRuleException : Exception
{
    public string ErrorType { get; }

    public BusinessRuleException(string errorType, string message) : base(message)
    {
        ErrorType = errorType;
    }
}
