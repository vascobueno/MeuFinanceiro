namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// Texto de um PDF, na ordem do conteúdo da página — o mesmo que se obtém copiando e colando.
/// A senha (faturas vêm protegidas) é usada só para abrir o documento: não é guardada, registrada
/// em log nem repassada.
/// </summary>
public interface IPdfTextExtractor
{
    /// <exception cref="Exceptions.BusinessRuleException">
    /// "pdf-protegido" sem senha, "pdf-senha-incorreta" com a senha errada, "pdf-invalido" se o
    /// arquivo não é um PDF legível.
    /// </exception>
    string ExtractText(byte[] pdf, string? password);
}
