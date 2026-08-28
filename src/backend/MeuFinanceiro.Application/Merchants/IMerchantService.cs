namespace MeuFinanceiro.Application.Merchants;

public interface IMerchantService
{
    /// <summary>
    /// Autocomplete por prefixo (StartsWith), priorizando frequência de uso e uso recente
    /// (RF005, regra 18, docs/05-regras-negocio.md). Debounce/limite de sugestões ficam no
    /// front-end (~300 ms, 10-15 itens) mas o backend já limita defensivamente.
    /// </summary>
    Task<IReadOnlyList<MerchantSuggestionResponse>> GetSuggestionsAsync(string prefix, int maxResults, CancellationToken ct = default);

    /// <summary>Cria o merchant se não existir e incrementa contagem de uso (chamado ao salvar um lançamento).</summary>
    Task<Guid> GetOrCreateAndTouchAsync(string description, CancellationToken ct = default);
}
