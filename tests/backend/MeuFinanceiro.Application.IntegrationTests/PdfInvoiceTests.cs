using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Reconciliation.Import;
using MeuFinanceiro.Infrastructure.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Fatura em PDF (docs/08). PDFs SINTÉTICOS, gerados aqui ou em Fixtures — nenhum dado real.
/// </summary>
public class PdfInvoiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PdfInvoiceTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly string Protegido = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fatura-sintetica-protegida.pdf");

    /// <summary>
    /// PDF com as linhas dadas (sem acento: a fonte padrão do gerador não tem "ç" nem "ã"). <paramref name="semEspacos"/> imita o PDF do Pan: cada palavra é
    /// posicionada à parte, sem caractere de espaço entre elas.
    /// </summary>
    private static byte[] Pdf(bool semEspacos, params string[] linhas)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        double y = 800;
        foreach (var linha in linhas)
        {
            if (!semEspacos)
            {
                page.AddText(linha, 10, new PdfPoint(40, y), font);
            }
            else
            {
                double x = 40;
                foreach (var palavra in linha.Split(' '))
                {
                    var letras = page.AddText(palavra, 10, new PdfPoint(x, y), font);
                    x = letras.Max(l => l.EndBaseLine.X) + 4; // vão de palavra, sem glifo de espaço
                }
            }
            y -= 14;
        }
        return builder.Build();
    }

    // ---------------- extração ----------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Separa_as_palavras_de_PDF_sem_caractere_de_espaco()
    {
        var texto = new PdfPigTextExtractor().ExtractText(Pdf(true, "Lancamentos do cartao adicional Portadora Ficticia | Cartao final 4032"), null);
        texto.Should().Contain("Lancamentos do cartao adicional Portadora Ficticia | Cartao final 4032");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Pdf_com_senha_abre_so_com_a_senha_certa_e_a_mensagem_nao_a_repete()
    {
        var bytes = File.ReadAllBytes(Protegido);
        var extrator = new PdfPigTextExtractor();

        extrator.ExtractText(bytes, "senha-de-teste").Should().Contain("FATURA SINTETICA");

        var semSenha = () => extrator.ExtractText(bytes, null);
        semSenha.Should().Throw<BusinessRuleException>().Which.ErrorType.Should().Be("pdf-protegido");

        var errada = () => extrator.ExtractText(bytes, "outra-senha");
        var erro = errada.Should().Throw<BusinessRuleException>().Which;
        erro.ErrorType.Should().Be("pdf-senha-incorreta");
        erro.Message.Should().NotContain("outra-senha");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Arquivo_que_nao_e_PDF_e_recusado()
    {
        var act = () => new PdfPigTextExtractor().ExtractText("não é pdf"u8.ToArray(), null);
        act.Should().Throw<BusinessRuleException>().Which.ErrorType.Should().Be("pdf-invalido");
    }

    // ---------------- resumo (os dois layouts) ----------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Resumo_no_formato_Santander()
    {
        const string texto = """
            valor Total a Pagar R$ 13.170,50
            Total a Pagar
            R$ 9.195,78
            Vencimento
            10/08/2026
            JUL. R$ 10.112,28 R$10.112,28 03/06/26 a 03/07/26
            AGO. R$ 9.195,78 Esta Fatura 04/07/26 a 03/08/26
            SET. R$ 817,82 Fatura Aberta 04/08/26 a 02/09/26
            Total desta Fatura R$
            9.195,78
            Saldo Anterior 10.112,28
            (=) Saldo Desta Fatura 9.195,78
            """;
        var r = InvoiceSummaryReader.Read(texto, new DateOnly(2026, 8, 3));

        // "Total a Pagar R$ 13.170,50" é a simulação do parcelamento — não é o total.
        r.Total.Should().Be(9195.78m);
        r.PreviousBalance.Should().Be(10112.28m);
        r.Due.Should().Be(new DateOnly(2026, 8, 10));
        r.Closing.Should().Be(new DateOnly(2026, 8, 3));
        r.NextClosing.Should().Be(new DateOnly(2026, 9, 2));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resumo_no_formato_Pan_nao_confunde_a_proxima_fatura_com_esta()
    {
        const string texto = """
            Fatura de agosto Limite total Vencimento
            R$ 54,00 R$ 40.990,00 12/08/2026
            Valor da fatura
            anterior (julho)
            R$ 54,00
            Total da fatura
            (agosto)
            R$ 54,00
            Próxima fatura | Setembro Despesas futuras
            Fechamento: 03/09 Soma das parcelas e transações de suas
            faturas nos próximos meses: R$ 432,00 Vencimento: 12/09
            > Compras e pagamentos feitos até o fechamento desta fatura (05/08/2026) Total: R$ 36,00
            """;
        var r = InvoiceSummaryReader.Read(texto, new DateOnly(2026, 8, 5));

        r.Total.Should().Be(54m);
        r.PreviousBalance.Should().Be(54m);
        r.Due.Should().Be(new DateOnly(2026, 8, 12));      // não o 12/09 da próxima
        r.Closing.Should().Be(new DateOnly(2026, 8, 5));   // não o 03/09 da próxima
        r.NextClosing.Should().Be(new DateOnly(2026, 9, 3));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void O_resumo_vale_mais_que_a_linha_solta_de_vencimento()
    {
        // Uma linha "Vencimento: 15/09" solta (de outro bloco) não pode vencer o vencimento do resumo.
        var r = InvoiceTextReader.Read("""
            Fatura de agosto Limite total Vencimento
            R$ 54,00 R$ 40.990,00 12/08/2026
            Vencimento: 15/09
            06/08 LOJA FICTICIA 10,00
            """, new DateOnly(2026, 8, 5));
        r.DueDateFromText.Should().Be(new DateOnly(2026, 8, 12));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Duas_colunas_separadas_por_um_espaco_viram_dois_itens()
    {
        var r = InvoiceTextReader.Read("06/07 Pagto Boleto Recebido -R$ 54,00 05/08 Anuidade Diferenc.titular04-12 R$ 36,00", new DateOnly(2026, 8, 5));
        r.Items.Should().HaveCount(2);
        r.Items[0].Kind.Should().Be(MeuFinanceiro.Domain.Enums.CardInvoiceItemKind.Payment);
        r.Items[1].InstallmentLabel.Should().Be("04/12");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tarifa_e_juros_de_saque_sem_data_entram_ligados_ao_saque()
    {
        var r = InvoiceTextReader.Read("""
            1 23/04 SAQUE FICTICIO 24H 100,00
            TARIFA RETIRADA - SAQUE 18,80
            IOF 0,52
            JUROS SAQUE 9,00
            """, new DateOnly(2026, 5, 4));

        r.Items.Should().HaveCount(4);
        r.Items.Skip(1).Should().OnlyContain(i => i.RelatedIndex == 0 && i.Date == new DateOnly(2026, 4, 23));
        r.ItemsSum.Should().Be(128.32m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ponto_e_virgula_fora_de_item_nao_vira_erro()
    {
        InvoiceTextReader.Read("gB5fJWvTuRP9x2c6hK;WY6wmR\n06/08 LOJA FICTICIA 10,00", new DateOnly(2026, 9, 2)).Errors.Should().BeEmpty();
    }

    // ---------------- endpoint ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário PDF", $"pdf{id}", $"pdf-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, byte[] pdf, string? senha)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "fatura.pdf");
        if (senha is not null) form.Add(new StringContent(senha), "password");
        return await client.PostAsync("/api/v1/card-invoices/parse-pdf", form);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Fatura_em_PDF_no_formato_Pan_e_lida_e_o_grupo_reconhecido()
    {
        var client = await NovoUsuarioAsync();
        var grupo = (JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/v1/cards", new
        {
            name = "Pan Ficticio", brand = "Visa", closingDay = 5, dueDay = 12, titular = new { kind = 1, lastFour = "4031" },
        })).Content.ReadAsStringAsync())).RootElement.GetProperty("id").GetGuid();

        var pdf = Pdf(true,
            "Fatura de agosto Limite total Vencimento",
            "R$ 54,00 R$ 40.990,00 12/08/2026",
            "Valor da fatura", "anterior (julho)", "R$ 54,00",
            "Total da fatura", "(agosto)", "R$ 54,00",
            "Lancamentos do cartao Portador Ficticio | Cartao final 4031",
            "> Compras e pagamentos feitos ate o fechamento desta fatura (05/08/2026) Total: R$ 36,00",
            "06/07 Pagto Boleto Recebido -R$ 54,00 05/08 Anuidade Diferenc.titular04-12 R$ 36,00",
            "Lancamentos do cartao adicional Portadora Ficticia | Cartao final 4032",
            "05/08 Anuidade Diferenc. Adi 04-12 R$ 18,00");

        var response = await EnviarAsync(client, pdf, null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var lido = JsonDocument.Parse(body).RootElement;

        lido.GetProperty("items").GetArrayLength().Should().Be(3);
        lido.GetProperty("totalFromText").GetDecimal().Should().Be(54m);
        lido.GetProperty("previousBalanceFromText").GetDecimal().Should().Be(54m);
        lido.GetProperty("closingDateFromText").GetString().Should().Be("2026-08-05");
        lido.GetProperty("dueDateFromText").GetString().Should().Be("2026-08-12");
        lido.GetProperty("detectedCreditCardId").GetGuid().Should().Be(grupo);
        lido.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("lastFour").GetString()).Should().Equal("4031", "4032");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PDF_com_senha_pelo_endpoint()
    {
        var client = await NovoUsuarioAsync();
        var bytes = File.ReadAllBytes(Protegido);

        var semSenha = await EnviarAsync(client, bytes, null);
        semSenha.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await semSenha.Content.ReadAsStringAsync()).Should().Contain("pdf-protegido");

        // Com a senha certa abre — mas o PDF sintético não tem itens, e isso é dito.
        var certa = await EnviarAsync(client, bytes, "senha-de-teste");
        var corpo = await certa.Content.ReadAsStringAsync();
        corpo.Should().Contain("pdf-sem-itens");
        corpo.Should().NotContain("senha-de-teste");
    }
}
