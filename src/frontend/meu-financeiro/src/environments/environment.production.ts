export const environment = {
  production: true,
  apiUrl: '/api/v1',
  autocompleteDebounceMs: 300,
  autocompleteMaxSuggestions: 15,
  // Preencher com a site key real do Cloudflare Turnstile antes do build de produção
  // (docs/19-deploy.md) — é um valor público, não um segredo, pode ir commitado/no bundle.
  turnstileSiteKey: '',
};
