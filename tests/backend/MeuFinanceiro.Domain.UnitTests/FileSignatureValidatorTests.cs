using FluentAssertions;
using MeuFinanceiro.Application.Attachments;
using Xunit;

namespace MeuFinanceiro.Domain.UnitTests;

/// <summary>
/// Cobre a allowlist de anexos (docs/12-seguranca.md).
///
/// O teste que importa é <see cref="Executavel_renomeado_para_pdf_deve_ser_recusado"/>: extensão e
/// Content-Type vêm do cliente, então sem a conferência de assinatura a allowlist não filtraria
/// nada — bastaria renomear o arquivo.
/// </summary>
[Trait("Category", "Unit")]
public class FileSignatureValidatorTests
{
    private static byte[] Header(params byte[] bytes)
    {
        // Preenche até o tamanho que o validador inspeciona, para simular um arquivo real.
        var buffer = new byte[FileSignatureValidator.HeaderBytesNeeded];
        bytes.CopyTo(buffer, 0);
        return buffer;
    }

    private static byte[] Ascii(string value) => System.Text.Encoding.ASCII.GetBytes(value);

    private static byte[] WithAsciiAt(int offset, string value, byte[]? into = null)
    {
        var buffer = into ?? new byte[FileSignatureValidator.HeaderBytesNeeded];
        Ascii(value).CopyTo(buffer, offset);
        return buffer;
    }

    // ---------------- tipos aceitos ----------------

    [Fact]
    public void Pdf_deve_ser_aceito()
    {
        var resolved = FileSignatureValidator.Resolve(Header(Ascii("%PDF-1.7")), "nota.pdf", out var error);

        resolved.Should().Be("application/pdf");
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("foto.jpeg")]
    [InlineData("FOTO.JPG")]
    public void Jpeg_deve_ser_aceito_em_qualquer_das_extensoes(string fileName)
    {
        var resolved = FileSignatureValidator.Resolve(Header(0xFF, 0xD8, 0xFF, 0xE0), fileName, out _);

        resolved.Should().Be("image/jpeg");
    }

    [Fact]
    public void Png_deve_ser_aceito()
    {
        var resolved = FileSignatureValidator.Resolve(
            Header(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A), "recibo.png", out _);

        resolved.Should().Be("image/png");
    }

    [Fact]
    public void Webp_deve_ser_aceito_apesar_do_tamanho_variavel_no_meio_do_cabecalho()
    {
        // RIFF....WEBP — os 4 bytes entre as duas marcas são o tamanho do arquivo e variam.
        var header = WithAsciiAt(0, "RIFF");
        header[4] = 0x2A; header[5] = 0x13; header[6] = 0x00; header[7] = 0x00;
        WithAsciiAt(8, "WEBP", header);

        FileSignatureValidator.Resolve(header, "comprovante.webp", out _).Should().Be("image/webp");
    }

    [Theory]
    [InlineData("heic")]
    [InlineData("heix")]
    [InlineData("mif1")]
    public void Heic_deve_ser_aceito_nas_marcas_conhecidas(string brand)
    {
        var header = WithAsciiAt(4, "ftyp");
        WithAsciiAt(8, brand, header);

        FileSignatureValidator.Resolve(header, "foto.heic", out _).Should().Be("image/heic");
    }

    // ---------------- recusas ----------------

    [Fact]
    public void Executavel_renomeado_para_pdf_deve_ser_recusado()
    {
        // "MZ" — cabeçalho de executável do Windows. É o caso que a conferência de assinatura
        // existe para pegar: a extensão diz .pdf e o Content-Type do multipart diria o que o
        // cliente quisesse.
        var resolved = FileSignatureValidator.Resolve(Header(0x4D, 0x5A, 0x90, 0x00), "nota-fiscal.pdf", out var error);

        resolved.Should().BeNull();
        error.Should().Contain("não é de um tipo aceito");
    }

    [Fact]
    public void Html_renomeado_para_png_deve_ser_recusado()
    {
        var resolved = FileSignatureValidator.Resolve(Header(Ascii("<html>")), "imagem.png", out var error);

        resolved.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Png_legitimo_com_extensao_de_pdf_deve_ser_recusado_por_incoerencia()
    {
        var resolved = FileSignatureValidator.Resolve(
            Header(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A), "documento.pdf", out var error);

        resolved.Should().BeNull();
        error.Should().Contain("não corresponde ao conteúdo");
    }

    [Fact]
    public void Arquivo_sem_extensao_deve_ser_recusado()
    {
        var resolved = FileSignatureValidator.Resolve(Header(Ascii("%PDF-1.7")), "documento", out var error);

        resolved.Should().BeNull();
        error.Should().Contain("sem extensão");
    }

    [Fact]
    public void Cabecalho_curto_demais_nao_deve_estourar()
    {
        // Um arquivo de 2 bytes é menor que qualquer assinatura — precisa recusar, não quebrar.
        var resolved = FileSignatureValidator.Resolve(new byte[] { 0xFF, 0xD8 }, "foto.jpg", out var error);

        resolved.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Conteudo_vazio_deve_ser_recusado()
    {
        FileSignatureValidator.Resolve(ReadOnlySpan<byte>.Empty, "foto.jpg", out var error).Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Allowlist_deve_expor_as_extensoes_aceitas()
    {
        FileSignatureValidator.AllowedExtensions
            .Should().Contain([".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic"]);
    }
}
