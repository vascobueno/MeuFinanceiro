namespace MeuFinanceiro.Application.Common;

/// <summary>
/// Query params padrão em toda listagem server-side (regra 19, docs/13-api.md):
/// GET /api/xxx?page=1&amp;pageSize=20&amp;sortBy=...&amp;sortDir=desc&amp;search=...
/// </summary>
public class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch { < 1 => 1, > 100 => 100, _ => value };
    }

    public string? SortBy { get; set; }
    public string SortDir { get; set; } = "asc";
    public string? Search { get; set; }

    public bool IsDescending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}
