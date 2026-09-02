namespace MeuFinanceiro.Api.Middleware;

/// <summary>
/// Cabeçalhos de segurança básicos, aplicados a toda resposta — defesa em profundidade além
/// do que um proxy reverso na frente (Nginx/Caddy/Cloudflare) possa já mandar em produção
/// (docs/12-seguranca.md).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
        await _next(context);
    }
}
