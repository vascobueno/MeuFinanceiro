# 14 — Angular

Organização:
```text
core/
shared/
layout/
features/
  auth/
  dashboard/
  transactions/
  accounts/
  cards/
  categories/
  installments/
  invoices/
  recurring/
  bank-reconciliation/
  card-reconciliation/
  investments/
  projection/
  settings/
  grupo/
  admin/
  reports/
  help/
```

Componentes reutilizáveis (`shared/components/`):
autocomplete, money-input, date-range, list-state, pagination, stat-card, status-badge,
timeline, help-dialog, intro-logo (animação de entrada da logo — `IntroAnimationService`
controla o modo: só login, splash global ou desativada; toca de novo a cada carregamento de
página e nunca com `prefers-reduced-motion`).

PrimeNG é a biblioteca de componentes padrão de toda a aplicação — `p-table` (com
`[customSort]` para grids paginadas/ordenadas server-side, ou
ordenação nativa do próprio componente para listas pequenas carregadas por completo),
`DynamicDialog`/`ConfirmationService` para diálogos, `p-drawer` (mobile) + `aside` (desktop)
no shell. Ícones customizados da marca via `MfIconComponent` (`shared/icons/`); ícones
utilitários (imprimir etc.) via PrimeIcons (`pi pi-*`).

Guards de rota em `core/guards/`: `authGuard` (autenticado), `adminGuard` (role Administrator),
`fullAccessGuard` (perfil de acesso Completo/Owner — telas de Configurações/Membros),
`canWriteGuard` (bloqueia Visualizador em rotas de formulário).

Não colocar segredo no frontend.
