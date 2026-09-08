namespace MeuFinanceiro.Application.Common.Exceptions;

/// <summary>Mapeada para HTTP 403 — falta de ownership/permissão (docs/12-seguranca.md, docs/13-api.md).</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Você não tem permissão para acessar este recurso.")
        : base(message)
    {
    }
}
