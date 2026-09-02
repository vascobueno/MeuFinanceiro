export const environment = {
  production: true,
  apiUrl: '/api/v1',
  autocompleteDebounceMs: 300,
  autocompleteMaxSuggestions: 15,
  // Preencher com a site key real do Cloudflare Turnstile antes do build de produção
  // (docs/19-deploy.md) — é um valor público, não um segredo, pode ir commitado/no bundle.
  turnstileSiteKey: '',
  // Preencher com uma license key PrimeNG própria pra produção antes do build (a chave de
  // dev gerada em primeui.dev não vale pro domínio de produção) — docs/19-deploy.md.
  primeNgLicenseKey: '',
};
