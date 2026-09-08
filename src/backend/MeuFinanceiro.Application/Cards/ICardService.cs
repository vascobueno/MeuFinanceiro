using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Cards;

public interface ICardService
{
    Task<PagedResult<CardResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default);
    Task<CardResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CardResponse> CreateAsync(CreateCardRequest request, CancellationToken ct = default);
    Task<CardResponse> UpdateAsync(Guid id, UpdateCardRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
