namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// Envio de e-mail (hoje só usado pela recuperação de senha). Implementação real é via SMTP
/// configurável (docs/23-configuracao-local.md); sem `Smtp:Host` configurado, a implementação
/// deve logar o conteúdo em vez de falhar.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
