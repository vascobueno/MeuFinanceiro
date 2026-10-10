using System.Text;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace MeuFinanceiro.Infrastructure.Pdf;

/// <summary>
/// Extrai o texto com PdfPig, na ordem do conteúdo de cada página — a ordem em que as faturas do
/// Santander e do Pan escrevem os itens, uma linha por item (validado contra as faturas reais).
///
/// A senha vive só nesta chamada: vai para as opções de abertura e some com elas. Nenhuma
/// mensagem de erro a repete.
/// </summary>
public class PdfPigTextExtractor : IPdfTextExtractor
{
    public string ExtractText(byte[] pdf, string? password)
    {
        try
        {
            var options = string.IsNullOrEmpty(password) ? new ParsingOptions() : new ParsingOptions { Password = password };
            using var document = PdfDocument.Open(pdf, options);

            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                AppendPage(text, page);
                text.AppendLine();
            }
            return text.ToString();
        }
        catch (PdfDocumentEncryptedException)
        {
            throw string.IsNullOrEmpty(password)
                ? new BusinessRuleException("pdf-protegido", "Este PDF tem senha. Informe a senha da fatura para lê-lo.")
                : new BusinessRuleException("pdf-senha-incorreta", "A senha não abriu o PDF. Confira e tente de novo.");
        }
        catch (Exception)
        {
            // A mensagem da biblioteca não volta para a tela: pode citar detalhes do arquivo.
            throw new BusinessRuleException("pdf-invalido", "Não foi possível ler este arquivo como PDF.");
        }
    }

    /// <summary>
    /// As letras na ordem do conteúdo, com espaço e quebra de linha deduzidos da posição. Há PDFs
    /// (o do Pan) que não têm caractere de espaço nenhum: as palavras são só letras afastadas, e a
    /// extração literal grudaria tudo ("Cartãofinal8156"). Espaço quando o vão passa de uma fração
    /// do corpo da letra; linha nova quando a linha de base muda ou o texto volta para a esquerda.
    /// </summary>
    private static void AppendPage(StringBuilder text, Page page)
    {
        Letter? previous = null;
        foreach (var letter in page.Letters)
        {
            if (previous is not null)
            {
                var size = Math.Max(previous.PointSize, 1);
                var sameLine = Math.Abs(letter.StartBaseLine.Y - previous.StartBaseLine.Y) < size * 0.3;
                var gap = letter.StartBaseLine.X - previous.EndBaseLine.X;

                if (!sameLine || gap < -size)
                {
                    text.AppendLine();
                }
                else if (gap > size * 0.15 && !string.IsNullOrWhiteSpace(letter.Value) && !char.IsWhiteSpace(LastChar(text)))
                {
                    text.Append(' ');
                }
            }
            text.Append(letter.Value);
            previous = letter;
        }
    }

    private static char LastChar(StringBuilder text) => text.Length == 0 ? ' ' : text[^1];
}
