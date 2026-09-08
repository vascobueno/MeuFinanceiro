using MeuFinanceiro.Application.Transactions;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Installments;

/// <summary>
/// Tela Parcelamento (docs/screens/parcelamento). Não duplica a lógica de criação de
/// parcelas: delega para o mesmo caminho já usado/testado em TransactionService
/// (InstallmentCalculator + criação de InstallmentPlan/Installment/FinancialTransaction),
/// só que exposto como recurso próprio (POST /api/v1/installment-plans), conforme pedido
/// pela tela dedicada em vez de passar pelo formulário geral de lançamento.
/// </summary>
public class InstallmentPlanService : IInstallmentPlanService
{
    private readonly ITransactionService _transactionService;

    public InstallmentPlanService(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task<InstallmentPlanResponse> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken ct = default)
    {
        var transactionRequest = new CreateTransactionRequest(
            request.FirstDueDate,
            request.Description,
            request.CategoryId,
            SubcategoryId: null,
            request.AccountId,
            request.CreditCardId,
            DestinationAccountId: null,
            TransactionType.Expense,
            TransactionDirection.Debit,
            Amount: request.Amount,
            Notes: null,
            Installment: new InstallmentRequest(request.Mode, request.Amount, request.InstallmentCount, request.FirstDueDate),
            Recurring: null
        );

        var created = await _transactionService.CreateAsync(transactionRequest, ct);
        var amounts = created.Select(t => t.Amount).ToList();

        return new InstallmentPlanResponse(amounts.Sum(), amounts.Count, amounts);
    }
}
