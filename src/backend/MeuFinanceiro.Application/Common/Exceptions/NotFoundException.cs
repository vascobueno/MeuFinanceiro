namespace MeuFinanceiro.Application.Common.Exceptions;

/// <summary>Mapeada para HTTP 404 pelo middleware de erros (docs/13-api.md).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} '{id}' não encontrado(a).")
    {
    }
}
