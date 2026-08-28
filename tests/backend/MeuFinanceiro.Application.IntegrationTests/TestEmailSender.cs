using MeuFinanceiro.Application.Common.Interfaces;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Substitui o SmtpEmailSender nos testes de integração — captura o último e-mail "enviado"
/// em memória, pra o teste extrair o token de reset de senha do link sem precisar de um SMTP
/// de verdade.
/// </summary>
public class TestEmailSender : IEmailSender
{
    public (string ToEmail, string Subject, string HtmlBody)? LastEmail { get; private set; }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        LastEmail = (toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
