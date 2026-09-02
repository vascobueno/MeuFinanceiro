using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MeuFinanceiro.Api.Filters;

/// <summary>
/// Bloqueia toda escrita (POST/PUT/PATCH/DELETE) para um usuário com AccessProfile
/// Visualizador — ponto central único em vez de checagem manual espalhada pelos ~10
/// controllers financeiros, mesmo raciocínio do override de auditoria em
/// AppDbContext.SaveChangesAsync. AuthController (login/refresh/logout precisam sempre
/// funcionar) e AdminController (papel de sistema, ortogonal ao AccessProfile pessoal do
/// grupo do administrador) ficam de fora.
/// </summary>
public class WriteAccessFilter : IAsyncActionFilter
{
    private static readonly string[] SafeMethods = { "GET", "HEAD", "OPTIONS" };
    private static readonly string[] ExemptControllers = { "Auth", "Admin" };

    private readonly ICurrentUserService _currentUser;

    public WriteAccessFilter(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isSafeMethod = SafeMethods.Contains(context.HttpContext.Request.Method, StringComparer.OrdinalIgnoreCase);
        var isExemptController = context.ActionDescriptor is ControllerActionDescriptor cad
            && ExemptControllers.Contains(cad.ControllerName, StringComparer.Ordinal);

        if (!isSafeMethod && !isExemptController && !_currentUser.CanWrite)
            throw new ForbiddenException("Seu perfil de acesso é somente leitura.");

        await next();
    }
}
