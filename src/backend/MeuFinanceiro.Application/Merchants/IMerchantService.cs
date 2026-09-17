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

    /// <summary>
    /// Versão em lote para importação: resolve todas as descrições de uma vez e NÃO salva —
    /// quem chama decide o momento do commit. A versão de item único faz uma consulta e um
    /// SaveChangesAsync por chamada, o que dentro de um laço de milhares de linhas vira um
    /// commit por linha. Devolve o mapa descrição normalizada → Id.
    /// </summary>
    Task<IReadOnlyDictionary<string, Guid>> GetOrCreateManyAsync(IReadOnlyCollection<string> descriptions, CancellationToken ct = default);
}
