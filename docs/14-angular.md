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

Espaçamento do cabeçalho das páginas: o vão entre o título e o que vem abaixo é a **soma de duas
margens** — a do `.mf-page-title` (global, `styles.scss`) e a do cabeçalho que o envolve,
`.mf-list-header` (16 cópias), `.mf-form-header` (2) ou `.mf-dashboard-header` (1), duplicados
porque estilo de componente não atravessa para o filho. Mexer em só uma delas corta um quarto, não
metade. A distância até a borda interna é o `padding` vertical do `.mf-content`
(`styles/_layout.scss`), separado do horizontal. Em cabeçalho que tem botão o título fica ~3px
abaixo do topo, porque `align-items: flex-end` com o botão (35px) mais alto que a linha do título
(32px) — daí 15px nessas páginas e 12px nas demais.

Não colocar segredo no frontend.
