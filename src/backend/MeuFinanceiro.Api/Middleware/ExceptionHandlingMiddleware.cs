using MeuFinanceiro.Application.Common.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Middleware;

/// <summary>
/// Converte exceções de negócio em ProblemDetails (RFC 7807), conforme docs/13-api.md:
/// regra de negócio -> 400, autenticação -> 401, ownership/permissão -> 403, não encontrado -> 404.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Nunca logar payload de senha/token — apenas metadados da exceção (docs/12-seguranca.md).
            _logger.LogError(ex, "Erro não tratado ao processar {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
    {
        var (status, title, type) = ex switch
        {
            BusinessRuleException bre => (StatusCodes.Status400BadRequest, bre.Message, $"https://meufinanceiro/erros/{bre.ErrorType}"),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message, "https://meufinanceiro/erros/nao-encontrado"),
            ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message, "https://meufinanceiro/erros/sem-permissao"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ex.Message, "https://meufinanceiro/erros/nao-autenticado"),
            // Título fixo (não ex.Message) só aqui — a mensagem real do Antiforgery é técnica
            // demais pro usuário final, e como Detail = ex.Message pra tudo que não é 500
            // (abaixo), a exceção real ainda aparece no Detail; o Title genérico é só a
            // camada de leitura rápida.
            AntiforgeryValidationException => (StatusCodes.Status400BadRequest, "Token de segurança inválido ou ausente.", "https://meufinanceiro/erros/csrf-invalido"),
            _ => (StatusCodes.Status500InternalServerError, "Ocorreu um erro inesperado.", "https://meufinanceiro/erros/interno")
        };

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = status == StatusCodes.Status500InternalServerError ? null : ex.Message,
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
