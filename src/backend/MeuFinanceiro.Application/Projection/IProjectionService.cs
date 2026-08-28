namespace MeuFinanceiro.Application.Projection;

public interface IProjectionService
{
    Task<ProjectionResponse> GetProjectionAsync(DateOnly date, CancellationToken ct = default);
}
