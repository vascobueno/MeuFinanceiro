using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>docs/17-auditoria.md — audita alterações sensíveis (nunca tokens/senhas).</summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityName, string entityId, object? oldValues, object? newValues, CancellationToken ct = default);

    /// <summary>Registra um evento de acesso (login/logout), sem entidade associada.</summary>
    Task LogAccessAsync(string action, Guid? userId, Guid? householdId, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>Owner vê só o audit log do próprio household; Administrator vê de todos (ver AuditController).</summary>
    Task<PagedResult<AuditLogResponse>> GetAllAsync(AuditLogFilter filter, CancellationToken ct = default);
}

public record AuditLogFilter(DateOnly? StartDate, DateOnly? EndDate, string? Action, Guid? HouseholdId, int Page = 1, int PageSize = 50);

public record AuditLogResponse(
    Guid Id, DateTime Timestamp, Guid? UserId, string? UserName, string Action, string EntityName, string EntityId, string? IpAddress);
