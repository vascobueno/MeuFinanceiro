using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre anexos de lançamento e de parcelamento (docs/06, docs/07, docs/12).
///
/// Os dois testes que sustentam o recurso são
/// <see cref="Anexo_de_outro_grupo_nao_deve_ser_baixado"/> — o projeto não tem query filter global
/// por grupo, então nada além do WHERE explícito impede que um GUID adivinhado vaze documento
/// alheio — e <see cref="Executavel_renomeado_para_pdf_deve_ser_recusado_pela_API"/>, que exercita
/// a conferência de assinatura ponta a ponta.
///
/// O storage aqui é o InMemoryFileStorage (o CI não sobe MinIO): cobre API, autorização e banco,
/// mas não o ida-e-volta real com o S3.
/// </summary>
[Trait("Category", "Integration")]
public class AttachmentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AttachmentEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ---------------- apoio ----------------

    private static string UsernameFromEmail(string email) => $"user{email.Split('@')[0]}";

    private async Task<(HttpClient client, UserResponse user)> NovoUsuarioAsync(string name = "Usuário Anexo")
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"{id}@teste.com";
        return await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, name, UsernameFromEmail(email), email, "SenhaForte!123");
    }

    /// <summary>Um PDF de mentira, mas com a assinatura verdadeira — é ela que o servidor confere.</summary>
    private static byte[] PdfBytes() => "%PDF-1.7\nconteudo de teste\n%%EOF"u8.ToArray();

    private static byte[] JpegBytes()
    {
        var bytes = new byte[64];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; bytes[3] = 0xE0;
        return bytes;
    }

    /// <summary>Cabeçalho "MZ" — executável do Windows.</summary>
    private static byte[] ExecutavelBytes()
    {
        var bytes = new byte[64];
        bytes[0] = 0x4D; bytes[1] = 0x5A;
        return bytes;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, string ownerQuery, byte[] content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        return await client.PostAsync($"/api/v1/attachments?{ownerQuery}", form);
    }

    private static async Task<Guid> CriarContaAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = $"Conta {Guid.NewGuid():N}"[..20],
            bank = "Banco Teste",
            type = 1,
            initialBalance = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarCartaoAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cards", new
        {
            name = $"Cartao {Guid.NewGuid():N}"[..20],
            brand = "Visa",
            closingDay = 5,
            dueDay = 15,
            creditLimit = 5000m,
        });
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarLancamentoAsync(HttpClient client, Guid accountId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            description = $"Compra {Guid.NewGuid():N}"[..20],
            accountId,
            type = 2,
            direction = 1,
            amount = 100m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement[0].GetProperty("id").GetGuid();
    }

    /// <summary>Cria um parcelamento de conta ou de cartão — os dois casos que o pedido distingue.</summary>
    private static async Task<Guid> CriarParcelamentoAsync(HttpClient client, Guid? accountId, Guid? creditCardId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/installment-plans", new
        {
            description = $"Geladeira {Guid.NewGuid():N}"[..20],
            accountId,
            creditCardId,
            mode = 1,
            amount = 1200m,
            installmentCount = 12,
            firstDueDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListarAsync(HttpClient client, string ownerQuery)
    {
        var response = await client.GetAsync($"/api/v1/attachments?{ownerQuery}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    // ---------------- ciclo completo ----------------

    [Fact]
    public async Task Upload_listagem_e_download_devem_devolver_o_mesmo_conteudo()
    {
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));
        var conteudo = PdfBytes();

        var upload = await UploadAsync(client, $"transactionId={lancamentoId}", conteudo, "nota fiscal.pdf");
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        var criado = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement;
        criado.GetProperty("fileName").GetString().Should().Be("nota fiscal.pdf");
        // O Content-Type gravado é o deduzido do conteúdo, não o "application/octet-stream" enviado.
        criado.GetProperty("contentType").GetString().Should().Be("application/pdf");
        criado.GetProperty("sizeBytes").GetInt64().Should().Be(conteudo.Length);

        var lista = await ListarAsync(client, $"transactionId={lancamentoId}");
        lista.GetArrayLength().Should().Be(1);

        var id = criado.GetProperty("id").GetGuid();
        var download = await client.GetAsync($"/api/v1/attachments/{id}/content");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(conteudo);
    }

    [Fact]
    public async Task Download_deve_vir_como_anexo_e_com_nosniff()
    {
        // Servir arquivo enviado por usuário inline é o caminho clássico de XSS armazenado.
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));

        var upload = await UploadAsync(client, $"transactionId={lancamentoId}", PdfBytes(), "nota.pdf");
        var id = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var download = await client.GetAsync($"/api/v1/attachments/{id}/content");

        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        download.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    [Fact]
    public async Task Varios_anexos_devem_conviver_no_mesmo_lancamento()
    {
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));

        (await UploadAsync(client, $"transactionId={lancamentoId}", PdfBytes(), "nota.pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UploadAsync(client, $"transactionId={lancamentoId}", JpegBytes(), "recibo.jpg")).StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await ListarAsync(client, $"transactionId={lancamentoId}");
        lista.GetArrayLength().Should().Be(2);
        lista.EnumerateArray().Select(a => a.GetProperty("contentType").GetString())
            .Should().BeEquivalentTo(["application/pdf", "image/jpeg"]);
    }

    // ---------------- parcelamento: conta E cartão ----------------

    [Fact]
    public async Task Parcelamento_de_conta_deve_aceitar_anexo()
    {
        var (client, _) = await NovoUsuarioAsync();
        var planoId = await CriarParcelamentoAsync(client, await CriarContaAsync(client), null);

        var upload = await UploadAsync(client, $"installmentPlanId={planoId}", PdfBytes(), "nota.pdf");

        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListarAsync(client, $"installmentPlanId={planoId}")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Parcelamento_de_cartao_deve_aceitar_anexo()
    {
        var (client, _) = await NovoUsuarioAsync();
        var planoId = await CriarParcelamentoAsync(client, null, await CriarCartaoAsync(client));

        var upload = await UploadAsync(client, $"installmentPlanId={planoId}", PdfBytes(), "nota.pdf");

        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListarAsync(client, $"installmentPlanId={planoId}")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Criacao_de_parcelamento_deve_devolver_o_id_do_plano()
    {
        // Sem este id a tela não tem a que anexar logo após salvar.
        var (client, _) = await NovoUsuarioAsync();

        var planoId = await CriarParcelamentoAsync(client, await CriarContaAsync(client), null);

        planoId.Should().NotBe(Guid.Empty);
    }

    // ---------------- contagem na listagem ----------------

    [Fact]
    public async Task Listagem_de_lancamentos_deve_trazer_a_contagem_de_anexos()
    {
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client);
        var comAnexo = await CriarLancamentoAsync(client, contaId);
        var semAnexo = await CriarLancamentoAsync(client, contaId);

        await UploadAsync(client, $"transactionId={comAnexo}", PdfBytes(), "nota.pdf");
        await UploadAsync(client, $"transactionId={comAnexo}", JpegBytes(), "recibo.jpg");

        var lista = await client.GetAsync($"/api/v1/transactions?page=1&pageSize=100&accountId={contaId}");
        lista.StatusCode.Should().Be(HttpStatusCode.OK);

        var itens = JsonDocument.Parse(await lista.Content.ReadAsStringAsync())
            .RootElement.GetProperty("items").EnumerateArray().ToList();

        itens.First(i => i.GetProperty("id").GetGuid() == comAnexo)
            .GetProperty("attachmentCount").GetInt32().Should().Be(2);
        itens.First(i => i.GetProperty("id").GetGuid() == semAnexo)
            .GetProperty("attachmentCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Parcela_deve_herdar_a_contagem_de_anexos_do_plano()
    {
        // A nota foi anexada ao parcelamento; sem herdar, as 12 parcelas apareceriam sem
        // documento na tela de Lançamentos.
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client);
        var planoId = await CriarParcelamentoAsync(client, contaId, null);

        await UploadAsync(client, $"installmentPlanId={planoId}", PdfBytes(), "nota.pdf");

        var lista = await client.GetAsync($"/api/v1/transactions?page=1&pageSize=100&accountId={contaId}");
        var itens = JsonDocument.Parse(await lista.Content.ReadAsStringAsync())
            .RootElement.GetProperty("items").EnumerateArray().ToList();

        itens.Should().HaveCount(12);
        itens.Should().OnlyContain(i => i.GetProperty("attachmentCount").GetInt32() == 1);
    }

    // ---------------- validação ----------------

    [Fact]
    public async Task Executavel_renomeado_para_pdf_deve_ser_recusado_pela_API()
    {
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));

        var upload = await UploadAsync(client, $"transactionId={lancamentoId}", ExecutavelBytes(), "nota-fiscal.pdf");

        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ListarAsync(client, $"transactionId={lancamentoId}")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Dono_ausente_ou_ambiguo_deve_ser_recusado()
    {
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));
        var planoId = await CriarParcelamentoAsync(client, await CriarContaAsync(client), null);

        (await UploadAsync(client, "", PdfBytes(), "nota.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await UploadAsync(client, $"transactionId={lancamentoId}&installmentPlanId={planoId}", PdfBytes(), "nota.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anexar_em_lancamento_inexistente_deve_dar_404()
    {
        var (client, _) = await NovoUsuarioAsync();

        var upload = await UploadAsync(client, $"transactionId={Guid.NewGuid()}", PdfBytes(), "nota.pdf");

        upload.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------- isolamento entre grupos ----------------

    [Fact]
    public async Task Anexo_de_outro_grupo_nao_deve_ser_baixado()
    {
        // O projeto não tem HasQueryFilter global por grupo: sem o WHERE explícito no service,
        // este GUID bastaria para baixar o documento alheio.
        var (clientA, _) = await NovoUsuarioAsync("Dono A");
        var lancamentoId = await CriarLancamentoAsync(clientA, await CriarContaAsync(clientA));
        var upload = await UploadAsync(clientA, $"transactionId={lancamentoId}", PdfBytes(), "sigiloso.pdf");
        var anexoId = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var (clientB, _) = await NovoUsuarioAsync("Estranho B");

        // 404, e não 403: um 403 confirmaria que aquele id existe.
        (await clientB.GetAsync($"/api/v1/attachments/{anexoId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clientB.DeleteAsync($"/api/v1/attachments/{anexoId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // E o dono continua conseguindo baixar — a restrição não quebrou o caso legítimo.
        (await clientA.GetAsync($"/api/v1/attachments/{anexoId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Listagem_nao_deve_enxergar_lancamento_de_outro_grupo()
    {
        var (clientA, _) = await NovoUsuarioAsync("Dono A");
        var lancamentoId = await CriarLancamentoAsync(clientA, await CriarContaAsync(clientA));
        await UploadAsync(clientA, $"transactionId={lancamentoId}", PdfBytes(), "sigiloso.pdf");

        var (clientB, _) = await NovoUsuarioAsync("Estranho B");

        (await clientB.GetAsync($"/api/v1/attachments?transactionId={lancamentoId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Visualizador_deve_baixar_mas_nao_anexar_nem_excluir()
    {
        var (clientOwner, _) = await NovoUsuarioAsync("Owner");
        var lancamentoId = await CriarLancamentoAsync(clientOwner, await CriarContaAsync(clientOwner));
        var upload = await UploadAsync(clientOwner, $"transactionId={lancamentoId}", PdfBytes(), "nota.pdf");
        var anexoId = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        // Membro do mesmo grupo, rebaixado a Visualizador.
        var id = Guid.NewGuid().ToString("N")[..12];
        var emailMembro = $"{id}@teste.com";
        var (_, membro) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Membro", UsernameFromEmail(emailMembro), emailMembro, "SenhaForte!123");

        (await clientOwner.PostAsJsonAsync("/api/v1/grupos/me/members",
            new AddMemberRequest(membro.Email, GrupoRole.Member))).StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, membroNoGrupo) = await AuthTestHelpers.LoginAsync(_factory, UsernameFromEmail(emailMembro), "SenhaForte!123");
        (await clientOwner.PutAsJsonAsync($"/api/v1/grupos/me/members/{membroNoGrupo.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(GrupoAccessProfile.Visualizador)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientVisualizador, _) = await AuthTestHelpers.LoginAsync(_factory, UsernameFromEmail(emailMembro), "SenhaForte!123");

        // Lê e baixa...
        (await clientVisualizador.GetAsync($"/api/v1/attachments?transactionId={lancamentoId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await clientVisualizador.GetAsync($"/api/v1/attachments/{anexoId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // ...mas não escreve.
        (await UploadAsync(clientVisualizador, $"transactionId={lancamentoId}", PdfBytes(), "outra.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clientVisualizador.DeleteAsync($"/api/v1/attachments/{anexoId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- exclusão ----------------

    [Fact]
    public async Task Excluir_anexo_deve_remover_a_linha_e_o_objeto_do_storage()
    {
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));
        var upload = await UploadAsync(client, $"transactionId={lancamentoId}", PdfBytes(), "nota.pdf");
        var anexoId = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var antes = _factory.FileStorage.Count;

        (await client.DeleteAsync($"/api/v1/attachments/{anexoId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListarAsync(client, $"transactionId={lancamentoId}")).GetArrayLength().Should().Be(0);
        // O objeto também: apagar só a linha deixaria lixo acumulando no storage para sempre.
        _factory.FileStorage.Count.Should().Be(antes - 1);
    }

    [Fact]
    public async Task Excluir_o_lancamento_deve_levar_os_anexos_e_os_objetos_junto()
    {
        var (client, _) = await NovoUsuarioAsync();
        var lancamentoId = await CriarLancamentoAsync(client, await CriarContaAsync(client));
        await UploadAsync(client, $"transactionId={lancamentoId}", PdfBytes(), "nota.pdf");
        await UploadAsync(client, $"transactionId={lancamentoId}", JpegBytes(), "recibo.jpg");

        var antes = _factory.FileStorage.Count;

        (await client.DeleteAsync($"/api/v1/transactions/{lancamentoId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // O cascade do banco apagaria as linhas, mas não os objetos — é o que este número guarda.
        _factory.FileStorage.Count.Should().Be(antes - 2);
    }
}
