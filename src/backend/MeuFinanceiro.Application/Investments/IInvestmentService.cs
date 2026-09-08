namespace MeuFinanceiro.Application.Investments;

public interface IInvestmentService
{
    Task<InvestmentsSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
    Task<InvestmentAccountResponse> CreateAccountAsync(CreateInvestmentAccountRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<InvestmentResponse>> GetInvestmentsAsync(Guid? investmentAccountId, CancellationToken ct = default);
    Task<InvestmentResponse> CreateInvestmentAsync(CreateInvestmentRequest request, CancellationToken ct = default);
    Task<InvestmentTransactionResponse> RegisterTransactionAsync(CreateInvestmentTransactionRequest request, CancellationToken ct = default);
}
