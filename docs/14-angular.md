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

A altura de um cabeçalho é ditada pelo **filho mais alto**, não pelo seu padding: encolher só o
espaçamento para por aí. No cabeçalho de painel das telas de Lançamentos a ação encolhe junto.

Nos cabeçalhos de **página** o compacto é o padrão, em todas as telas (`styles.scss`): título em
0.9rem sem margem inferior, ícone em 1.3rem e ação com altura fixa de 1.45rem — fixada, e não
caçada por padding, porque nessa escala o cálculo do tema erra 1-2px. O `.mf-page-title` tem
`min-height` igual à da ação, para a faixa medir o mesmo com ou sem botão: 23,2px em qualquer
página, com 12px até a borda interna e 8px até o objeto seguinte.

O cabeçalho das **grades** acompanha a mesma altura, por token do tema
(`datatable.headerCell.padding` e `.sm.padding` em `app/styles/primeng-preset.ts`), e não por
CSS: a regra do tema `.p-datatable-thead > tr > th` tem a mesma especificidade de qualquer
seletor nosso e vence por vir depois na cascata. Antes conviviam três alturas — 38px nas grades
sem `size="small"`, 26px nas com, e o cabeçalho de página.

> Duas pegadinhas de cascata aqui. O `mf-icon` fixa 24px no próprio `:host`, que empata em
> especificidade com `.mf-page-title-icon` e vence por vir depois — por isso o seletor é composto
> (`.mf-page-title .mf-page-title-icon`). E era o ícone, não a fonte, que segurava a altura.

Não colocar segredo no frontend.
