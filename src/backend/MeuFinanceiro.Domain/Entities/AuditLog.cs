namespace MeuFinanceiro.Domain.Entities;

/// <summary>docs/17-auditoria.md — nunca guardar tokens ou senhas na auditoria.</summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    /// <summary>Grupo do dado alterado (entidades grupo-owned) ou do usuário que logou (eventos de acesso). Null quando não aplicável (ex.: login falhou e o e-mail não existe).</summary>
    public Guid? GrupoId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Accept, Undo, Login, LoginFailed...
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}
