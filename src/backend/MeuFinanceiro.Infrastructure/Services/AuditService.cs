using System.Text.Json;
using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Infrastructure.Identity;
using MeuFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>docs/17-auditoria.md — nunca serializa tokens/senhas (não fazem parte das entidades de domínio).</summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditService(AppDbContext db, ICurrentUserService currentUser, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task LogAsync(string action, string entityName, string entityId, object? oldValues, object? newValues, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            GrupoId = _currentUser.IsAuthenticated ? _currentUser.GrupoId : null,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CorrelationId = Guid.NewGuid().ToString()
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task LogAccessAsync(string action, Guid? userId, Guid? grupoId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            GrupoId = grupoId,
            Action = action,
            EntityName = nameof(ApplicationUser),
            EntityId = userId?.ToString() ?? string.Empty,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = Guid.NewGuid().ToString()
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogResponse>> GetAllAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        // Visibilidade: Administrator vê tudo (com filtro opcional por grupo); qualquer
        // outro usuário vê só o audit log do próprio grupo.
        if (_currentUser.IsAdministrator)
        {
            if (filter.GrupoId is not null)
                query = query.Where(a => a.GrupoId == filter.GrupoId);
        }
        else
        {
            query = query.Where(a => a.GrupoId == _currentUser.GrupoId);
        }

        if (filter.StartDate is not null)
        {
            var start = filter.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.Timestamp >= start);
        }
        if (filter.EndDate is not null)
        {
            var end = filter.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(a => a.Timestamp <= end);
        }
        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(a => a.Action == filter.Action);

        query = query.OrderByDescending(a => a.Timestamp);

        var totalItems = await query.CountAsync(ct);
        var logs = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);

        var userIds = logs.Where(l => l.UserId is not null).Select(l => l.UserId!.Value).Distinct().ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var items = logs
            .Select(l => new AuditLogResponse(
                l.Id, l.Timestamp, l.UserId,
                l.UserId is not null ? users.GetValueOrDefault(l.UserId.Value)?.Name : null,
                l.Action, l.EntityName, l.EntityId, l.IpAddress))
            .ToList();

        return PagedResult<AuditLogResponse>.Create(items, filter.Page, filter.PageSize, totalItems);
    }
}
