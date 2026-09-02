using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MeuFinanceiro.Api.Filters;

/// <summary>
/// Validação de CSRF (double-submit cookie — suporte nativo do ASP.NET Core e do HttpClient
/// do Angular, sem código customizado dos dois lados). Necessária porque a autenticação
/// agora é por cookie httpOnly (SameSite=Strict já bloqueia a maioria dos ataques, isso é a
/// segunda camada). AuthController fica de fora: login/registro precisam funcionar antes de
/// existir qualquer cookie CSRF (docs/12-seguranca.md).
/// </summary>
public class CsrfFilter : IAsyncActionFilter
{
    private static readonly string[] SafeMethods = { "GET", "HEAD", "OPTIONS" };
    private static readonly string[] ExemptControllers = { "Auth" };

    private readonly IAntiforgery _antiforgery;

    public CsrfFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isSafeMethod = SafeMethods.Contains(context.HttpContext.Request.Method, StringComparer.OrdinalIgnoreCase);
        var isExemptController = context.ActionDescriptor is ControllerActionDescriptor cad
            && ExemptControllers.Contains(cad.ControllerName, StringComparer.Ordinal);

        if (!isSafeMethod && !isExemptController)
            await _antiforgery.ValidateRequestAsync(context.HttpContext);

        await next();
    }
}
