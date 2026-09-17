namespace MeuFinanceiro.Application.Projection;

public interface IProjectionService
{
    /// <summary>
    /// maxTimelineEntries limita quantas entradas da linha do tempo voltam na resposta; os
    /// agregados (TimelineCount, ProjectedExpensesTotal) continuam sobre a linha completa. Serve
    /// para quem só precisa dos números e de uma prévia, como o Dashboard.
    /// </summary>
    Task<ProjectionResponse> GetProjectionAsync(DateOnly date, int? maxTimelineEntries = null, CancellationToken ct = default);
}
