using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Reconciliation.Import;
using MeuFinanceiro.Domain.Entities;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Leitura do OFX e importação pela conta reconhecida (docs/09). Arquivos sintéticos.</summary>
public class OfxImportTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OfxImportTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>OFX 1.x: cabeçalho de texto e tags de valor sem fechamento.</summary>
    private static string Sgml(string acctId = "12345-6", string branch = "0001", params (string fitId, string date, string amount, string memo)[] trns)
    {
        var sb = new StringBuilder("""
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:102
            ENCODING:USASCII
            CHARSET:1252

            <OFX>
            <BANKMSGSRSV1><STMTTRNRS><STMTRS>
            <CURDEF>BRL
            """);
        sb.Append($"\n<BANKACCTFROM>\n<BANKID>0033\n<BRANCHID>{branch}\n<ACCTID>{acctId}\n<ACCTTYPE>CHECKING\n</BANKACCTFROM>\n");
        sb.Append("<BANKTRANLIST>\n<DTSTART>20230101000000[-3:BRT]\n<DTEND>20230131000000[-3:BRT]\n");
        foreach (var t in trns)
            sb.Append($"<STMTTRN>\n<TRNTYPE>OTHER\n<DTPOSTED>{t.date}120000[-3:BRT]\n<TRNAMT>{t.amount}\n<FITID>{t.fitId}\n<MEMO>{t.memo}\n</STMTTRN>\n");
        sb.Append("</BANKTRANLIST>\n<LEDGERBAL>\n<BALAMT>1000.00\n<DTASOF>20230131\n</LEDGERBAL>\n</STMTRS></STMTTRNRS></BANKMSGSRSV1>\n</OFX>\n");
        return sb.ToString();
    }

    private static readonly (string, string, string, string)[] Janeiro =
    [
        ("F1", "20230108", "-18.00", "COMPRA CARTAO DEB MC 02/01 LOJA FICTICIA A"),
        ("F2", "20230110", "-50,00", "PIX ENVIADO PORTADOR FICTICIO"),
        ("F3", "20230115", "300.00", "PIX RECEBIDO EMPRESA FICTICIA"),
    ];

    // ---------------- leitor ----------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Le_conta_periodo_saldo_e_movimentos_do_OFX_1()
    {
        var ofx = OfxStatementReader.Read(Sgml(trns: Janeiro));

        ofx.Errors.Should().BeEmpty();
        (ofx.BankId, ofx.BranchId, ofx.AccountId).Should().Be(("0033", "0001", "12345-6"));
        ofx.Start.Should().Be(new DateOnly(2023, 1, 1));
        ofx.End.Should().Be(new DateOnly(2023, 1, 31));
        ofx.LedgerBalance.Should().Be(1000m);

        ofx.Items.Should().HaveCount(3);
        ofx.Items[0].ExternalId.Should().Be("F1");
        ofx.Items[0].ItemType.Should().Be("DEBIT_CARD_PURCHASE");
        // Data da compra no MEMO, como no extrato colado.
        ofx.Items[0].EffectiveDate.Should().Be(new DateOnly(2023, 1, 2));
        ofx.Items[0].Description.Should().Be("COMPRA CARTAO DEB MC LOJA FICTICIA A");
        // Vírgula decimal, que alguns bancos usam no TRNAMT.
        ofx.Items[1].Amount.Should().Be(-50m);
        ofx.Items[2].ItemType.Should().Be("PIX_RECEIVED");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Le_OFX_2_em_XML()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <?OFX OFXHEADER="200" VERSION="211"?>
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
            <BANKACCTFROM><BANKID>341</BANKID><ACCTID>998877</ACCTID></BANKACCTFROM>
            <BANKTRANLIST><DTSTART>20230301</DTSTART><DTEND>20230331</DTEND>
            <STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20230305</DTPOSTED><TRNAMT>-9.90</TRNAMT><FITID>X1</FITID><NAME>TARIFA</NAME><MEMO>PACOTE SERVIÇOS</MEMO></STMTTRN>
            </BANKTRANLIST>
            <LEDGERBAL><BALAMT>90.10</BALAMT></LEDGERBAL>
            </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;
        var ofx = OfxStatementReader.Read(xml);

        ofx.Errors.Should().BeEmpty();
        ofx.BranchId.Should().BeNull();
        ofx.Items.Single().Description.Should().Be("TARIFA PACOTE SERVIÇOS");
        ofx.Items.Single().ItemType.Should().Be("BANK_FEE");
        ofx.LedgerBalance.Should().Be(90.10m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decodifica_Windows_1252_sem_estragar_acento()
    {
        var bytes = Encoding.Latin1.GetBytes("CHARSET:1252\n<OFX><STMTTRN><DTPOSTED>20230101<TRNAMT>-1.00<MEMO>AÇÃO</STMTTRN></OFX>");
        OfxStatementReader.Read(OfxStatementReader.Decode(bytes)).Items.Single().Description.Should().Be("AÇÃO");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Arquivo_que_nao_e_OFX_explica_o_motivo()
    {
        OfxStatementReader.Read("data;descrição;valor").Errors.Single().Message.Should().Contain("Não parece um arquivo OFX");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("0033", "0001", "12345-6", true)]
    [InlineData("033", null, "123456", true)]
    [InlineData(null, null, "12345", true)]
    [InlineData("0033", null, "0001123456", true)]   // agência na frente
    [InlineData("0341", "0001", "12345-6", false)]   // outro banco
    [InlineData("0033", "0002", "12345-6", false)]   // outra agência
    [InlineData("0033", "0001", "12346-6", false)]
    public void Reconhece_a_conta_pelas_formas_que_os_bancos_usam(string? bank, string? branch, string acct, bool esperado)
    {
        var conta = new Account { BankCode = "033", Agency = "0001", AccountNumber = "12345", AccountDigit = "6" };
        OfxAccountMatcher.Matches(conta, bank, branch, acct).Should().Be(esperado);
    }

    // ---------------- endpoint ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário OFX", $"ofx{id}", $"ofx-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> CriarContaAsync(HttpClient client, string numero = "12345") =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = $"Conta {numero}", bank = "Santander", type = 1, initialBalance = 0m,
            bankCode = "033", agency = "1", accountNumber = numero, accountDigit = "6",
        }))).GetProperty("id").GetGuid();

    private static async Task<JsonElement> LerAsync(HttpClient client, string ofx)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(ofx));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/x-ofx");
        form.Add(file, "file", "extrato.ofx");
        return await JsonAsync(await client.PostAsync("/api/v1/bank-statements/parse-ofx", form));
    }

    private static Task<HttpResponseMessage> ImportarAsync(HttpClient client, JsonElement lido) =>
        client.PostAsJsonAsync("/api/v1/bank-statements/import", new
        {
            accountId = lido.GetProperty("accountId").GetGuid(),
            referenceStart = lido.GetProperty("referenceStart").GetString(),
            referenceEnd = lido.GetProperty("referenceEnd").GetString(),
            openingBalance = lido.GetProperty("openingBalance").GetDecimal(),
            closingBalance = lido.GetProperty("closingBalance").GetDecimal(),
            originalFileName = "extrato.ofx",
            items = lido.GetProperty("items"),
        });

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OFX_reconhece_a_conta_calcula_o_saldo_inicial_e_importa()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client);

        var lido = await LerAsync(client, Sgml(trns: Janeiro));
        lido.GetProperty("accountId").GetGuid().Should().Be(contaId);
        lido.GetProperty("accountFromFile").GetString().Should().Be("banco 0033, agência 0001, conta 12345-6");
        // 1000 − (−18 − 50 + 300) = 768.
        lido.GetProperty("openingBalance").GetDecimal().Should().Be(768m);
        lido.GetProperty("alreadyImported").GetInt32().Should().Be(0);

        var importado = await JsonAsync(await ImportarAsync(client, lido));
        importado.GetProperty("itemCount").GetInt32().Should().Be(3);
        importado.GetProperty("balanceValidated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OFX_que_se_sobrepoe_so_importa_os_movimentos_novos()
    {
        var client = await NovoUsuarioAsync();
        await CriarContaAsync(client);
        await JsonAsync(await ImportarAsync(client, await LerAsync(client, Sgml(trns: Janeiro[..2]))));

        // Segundo download, do mês inteiro: F1 e F2 de novo, mais F3.
        var lido = await LerAsync(client, Sgml(trns: Janeiro));
        lido.GetProperty("alreadyImported").GetInt32().Should().Be(2);

        var importado = await JsonAsync(await ImportarAsync(client, lido));
        importado.GetProperty("itemCount").GetInt32().Should().Be(1);
        importado.GetProperty("skippedDuplicates").GetInt32().Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OFX_sem_nada_novo_e_recusado()
    {
        var client = await NovoUsuarioAsync();
        await CriarContaAsync(client);
        await JsonAsync(await ImportarAsync(client, await LerAsync(client, Sgml(trns: Janeiro))));

        // Mesmos movimentos em outro arquivo (saldo diferente: não é o mesmo conteúdo).
        var outro = Sgml(trns: Janeiro).Replace("<BALAMT>1000.00", "<BALAMT>2000.00");
        var response = await ImportarAsync(client, await LerAsync(client, outro));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("extrato-sem-movimentos-novos");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Conta_de_outro_grupo_nao_e_reconhecida()
    {
        var outro = await NovoUsuarioAsync();
        await CriarContaAsync(outro);

        var client = await NovoUsuarioAsync();
        var lido = await LerAsync(client, Sgml(trns: Janeiro));
        lido.GetProperty("accountId").ValueKind.Should().Be(JsonValueKind.Null);
        lido.GetProperty("matchingAccounts").GetInt32().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FITID_de_outra_conta_nao_conta_como_ja_importado()
    {
        var client = await NovoUsuarioAsync();
        await CriarContaAsync(client, "12345");
        await CriarContaAsync(client, "77777");
        await JsonAsync(await ImportarAsync(client, await LerAsync(client, Sgml(trns: Janeiro))));

        // Mesmos FITIDs, outra conta: são outros movimentos.
        var lido = await LerAsync(client, Sgml(acctId: "77777-6", trns: Janeiro));
        lido.GetProperty("alreadyImported").GetInt32().Should().Be(0);
    }
}
