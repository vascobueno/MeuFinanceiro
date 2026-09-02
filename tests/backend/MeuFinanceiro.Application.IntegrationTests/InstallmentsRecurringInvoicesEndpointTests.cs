using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Installments;
using MeuFinanceiro.Application.Invoices;
using MeuFinanceiro.Application.Recurring;
using MeuFinanceiro.Application.Transactions;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre as telas Parcelamento, Recorrência e Faturas (docs/screens), incluindo a regra 13
/// (recorrência: nova versão por data de vigência, sem alterar o valor vigente hoje) e as
/// regras 15/16/17 (parcelamento: soma fecha o principal, arredondamento na última parcela).
/// </summary>
[Trait("Category", "Integration")]
public class InstallmentsRecurringInvoicesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public InstallmentsRecurringInvoicesEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    private async Task AuthenticateAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"user-{id}@teste.com";
        var username = $"user{id}";

        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Usuário Teste", username, email, "SenhaForte!123"));

        await AuthTestHelpers.ConfirmEmailAsync(_client, _factory.EmailSender, email);

        await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "SenhaForte!123"));
        await AuthTestHelpers.AttachCsrfTokenAsync(_client);
    }

    private async Task<Guid> CreateAccountAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Corrente Teste",
            bank = "Banco Teste",
            type = 1,
            initialBalance = 0m
        });
        var account = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return account.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateCreditCardAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/cards", new
        {
            name = "Cartão Teste",
            brand = "Visa",
            closingDay = 10,
            dueDay = 20,
            limit = 5000m
        });
        var card = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return card.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Parcelamento_deve_fechar_soma_com_arredondamento_na_ultima_parcela()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/installment-plans", new CreateInstallmentPlanRequest(
            "Compra parcelada teste", accountId, null, null,
            MeuFinanceiro.Domain.Enums.InstallmentMode.TotalAmount, 1000m, 3, DateOnly.FromDateTime(DateTime.UtcNow)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<InstallmentPlanResponse>();

        result!.InstallmentCount.Should().Be(3);
        result.InstallmentAmounts.Sum().Should().Be(1000m);
        result.InstallmentAmounts[0].Should().Be(333.33m);
        result.InstallmentAmounts[1].Should().Be(333.33m);
        result.InstallmentAmounts[2].Should().Be(333.34m); // regra 17: arredondamento na última parcela
    }

    [Fact]
    public async Task Recorrencia_alteracao_futura_nao_deve_mudar_o_valor_vigente_hoje()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/transactions", new CreateTransactionRequest(
            today, "Água", null, null, accountId, null, null,
            MeuFinanceiro.Domain.Enums.TransactionType.Expense, MeuFinanceiro.Domain.Enums.TransactionDirection.Debit,
            150m, null, null,
            new RecurringRequest(MeuFinanceiro.Domain.Enums.RecurrenceFrequency.Monthly, null)));

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        var recurringId = created!.Single().RecurringTransactionId!.Value;

        var beforeResponse = await _client.GetAsync($"/api/v1/recurring-transactions/{recurringId}");
        var before = await beforeResponse.Content.ReadFromJsonAsync<RecurringTransactionResponse>();
        before!.CurrentAmount.Should().Be(150m);

        var newVersionResponse = await _client.PostAsJsonAsync(
            $"/api/v1/recurring-transactions/{recurringId}/versions",
            new CreateRecurringVersionRequest(today.AddMonths(1), 175m));

        newVersionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterCreatingFutureVersion = await newVersionResponse.Content.ReadFromJsonAsync<RecurringTransactionResponse>();

        // Regra 13: a versão só passa a valer a partir de EffectiveFrom — hoje continua 150.
        afterCreatingFutureVersion!.CurrentAmount.Should().Be(150m);
    }

    [Fact]
    public async Task Recorrencia_nao_deve_aceitar_nova_versao_com_data_anterior_ou_igual_a_vigente()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/transactions", new CreateTransactionRequest(
            today, "Água", null, null, accountId, null, null,
            MeuFinanceiro.Domain.Enums.TransactionType.Expense, MeuFinanceiro.Domain.Enums.TransactionDirection.Debit,
            150m, null, null,
            new RecurringRequest(MeuFinanceiro.Domain.Enums.RecurrenceFrequency.Monthly, null)));

        var created = await createResponse.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        var recurringId = created!.Single().RecurringTransactionId!.Value;

        var invalidVersionResponse = await _client.PostAsJsonAsync(
            $"/api/v1/recurring-transactions/{recurringId}/versions",
            new CreateRecurringVersionRequest(today, 175m));

        invalidVersionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Faturas_deve_listar_fatura_importada_com_status_derivado_do_vencimento()
    {
        await AuthenticateAsync();
        var cardId = await CreateCreditCardAsync();
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

        var importResponse = await _client.PostAsJsonAsync("/api/v1/card-invoices/import", new
        {
            creditCardId = cardId,
            closingDate = dueDate.AddDays(-15),
            dueDate,
            totalAmount = 250.00m,
            originalFileName = "fatura-teste.pdf",
            items = new[]
            {
                new { date = dueDate.AddDays(-20), description = "Loja Teste", installmentLabel = (string?)null, amount = 250.00m }
            }
        });
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await _client.GetAsync("/api/v1/card-invoices");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await listResponse.Content.ReadFromJsonAsync<PagedResultDto>();
        page!.Items.Should().ContainSingle();
        page.Items[0].Status.Should().Be(CardInvoiceStatus.Aberta);
        page.Items[0].TotalAmount.Should().Be(250.00m);
    }

    private record PagedResultDto(List<CardInvoiceListItemResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
}
