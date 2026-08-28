# Design System Material do MeuFinanceiro

O frontend usa Angular Material como biblioteca de componentes e um design system próprio para identidade financeira.

## Princípios

- Material para componentes, interação e acessibilidade;
- identidade MeuFinanceiro em tokens centralizados;
- sidebar/drawer;
- toolbar;
- cards financeiros;
- tabelas densas e legíveis;
- filtros avançados;
- chips para estados;
- dialogs para operações críticas;
- snackbar para feedback curto;
- responsividade;
- suporte futuro a dark mode.

## Componentes

Os mockups em `docs/screens` devem ser implementados usando componentes Material equivalentes.

Não copiar o HTML do mockup diretamente para produção.

## Telas implementadas

Cada rota carrega `data.title` (breadcrumb + título do diálogo de ajuda), `data.help` (texto do
diálogo de ajuda) e, quando a tela é conceitualmente filha de uma lista, `data.parent` (segundo
nível clicável do breadcrumb) — ver `app.routes.ts`.

| Rota | Componente | Tela |
|---|---|---|
| `/login` | `LoginComponent` | Login |
| `/dashboard` | `DashboardComponent` | Visão geral |
| `/lancamentos` | `TransactionListComponent` | Lançamentos |
| `/lancamentos/novo` | `TransactionFormComponent` | Novo lançamento |
| `/lancamentos/:id` | `TransactionFormComponent` | Editar lançamento |
| `/contas` | `AccountListComponent` | Contas correntes |
| `/parcelamento` | `InstallmentFormComponent` | Parcelamento |
| `/recorrencia` | `RecurringListComponent` | Recorrências |
| `/recorrencia/:id` | `RecurringFormComponent` | Recorrência |
| `/faturas` | `InvoiceListComponent` | Faturas |
| `/cartoes` | `CardListComponent` | Cartões |
| `/categorias` | `CategoryListComponent` | Categorias |
| `/conciliacao-conta` | `BankReconciliationComponent` | Conciliação bancária |
| `/conciliacao-cartao` | `CardReconciliationComponent` | Conciliação de cartão |
| `/investimentos` | `InvestmentsComponent` | Investimentos |
| `/projecao` | `ProjectionComponent` | Projeção financeira |
| `/relatorios/projecao-receitas` | `IncomeProjectionReportComponent` | Projeção de receitas |
| `/relatorios/gastos-recorrentes` | `RecurringExpensesReportComponent` | Gastos recorrentes |
| `/relatorios/auditoria` | `AuditLogComponent` | Auditoria |
| `/configuracoes` | `SettingsComponent` | Configurações |
| `/membros` | `HouseholdMembersComponent` | Membros da família |
| `/registrar` | `RegisterComponent` | Criar conta (fora do shell, sem autenticação) |
| `/admin/households` | `AdminHouseholdsComponent` | Households (Administrator) |
| `/admin/perfis-acesso` | `AdminAccessProfilesComponent` | Perfis de acesso (Administrator) |
| `/admin/households/:id/membros` | `HouseholdMembersComponent` (reaproveitado) | Membros do household (Administrator) |
| `/ajuda` | `HelpPageComponent` | Ajuda geral — todas as telas |

`/recorrencia/:id` é alcançável tanto por `/recorrencia` (lista) quanto por uma linha de
`/lancamentos` ("Editar recorrência") — por isso o botão Voltar dessa tela usa
`Location.back()` em vez de um destino fixo.
