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
  household/
  admin/
  reports/
  help/
```

Componentes reutilizáveis (`shared/components/`):
autocomplete, money-input, date-range, list-state, pagination, stat-card, status-badge,
timeline, confirm-dialog, help-dialog.

Angular Material é a biblioteca de componentes padrão de toda a aplicação — `mat-table` +
`mat-sort` + `mat-paginator` (ou `MatTableDataSource` + `MatSort` client-side para listas
pequenas) em todas as grids, `mat-dialog` para diálogos, `mat-sidenav`/`mat-toolbar` no shell.

Guards de rota em `core/guards/`: `authGuard` (autenticado), `adminGuard` (role Administrator),
`fullAccessGuard` (perfil de acesso Completo/Owner — telas de Configurações/Membros),
`canWriteGuard` (bloqueia Visualizador em rotas de formulário).

Não colocar segredo no frontend.
