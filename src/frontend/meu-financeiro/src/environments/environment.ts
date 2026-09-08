// Substituído em build de produção por environment.production.ts (fileReplacements no angular.json).
export const environment = {
  production: false,
  apiUrl: 'https://localhost:5001/api/v1',
  autocompleteDebounceMs: 300,
  autocompleteMaxSuggestions: 15,
  // Chave pública do Cloudflare Turnstile (docs/23-configuracao-local.md) — vazia = o
  // widget de CAPTCHA não aparece; sem Turnstile:SecretKey configurado no backend, isso não
  // bloqueia nada (mesmo padrão de degradação graciosa do SMTP).
  turnstileSiteKey: '',
  primeNgLicenseKey: '',
};
