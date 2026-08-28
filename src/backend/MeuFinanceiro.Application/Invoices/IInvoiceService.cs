using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Invoices;

public interface IInvoiceService
{
    Task<PagedResult<CardInvoiceListItemResponse>> GetAllAsync(CardInvoiceFilter filter, CancellationToken ct = default);
}
