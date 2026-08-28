namespace MeuFinanceiro.Domain.Enums;

/// <summary>docs/16-importacao-inicial.md — fluxo Excel -> Upload -> Validação -> Prévia -> Importação -> Relatório.</summary>
public enum ImportStatus
{
    Uploaded = 1,
    Validated = 2,
    Previewed = 3,
    Imported = 4,
    Failed = 5
}
