# Design System PrimeNG + Tailwind do MeuFinanceiro

O frontend usa PrimeNG como biblioteca de componentes, Tailwind CSS para layout/utilitários
e um design system próprio para identidade financeira.

## Princípios

- PrimeNG para componentes/interação, Tailwind para layout/spacing/utilitários;
- identidade MeuFinanceiro em tokens centralizados (`--mf-*`, `styles/_tokens.scss`), lidos
  também pelo preset do PrimeNG (`styles/primeng-preset.ts`) — trocar skin retema os dois
  de uma vez, sem lógica JS adicional;
- sidebar fixa (desktop) / `p-drawer` (mobile);
- cards financeiros (`.mf-card`);
- tabelas densas e legíveis (`p-table`, ordenação server-side via `[customSort]` ou nativa
  para listas pequenas);
- filtros avançados;
- badges para estados (`.mf-badge`, span próprio — não usa nenhum componente de chip);
- `DynamicDialog`/`ConfirmationService` para operações críticas;
- feedback de formulário via banner inline (`.mf-form-error`/`.mf-form-success`), não toast;
- responsividade;
- suporte futuro a dark mode.

## Componentes

Os mockups em `docs/screens` devem ser implementados usando componentes PrimeNG equivalentes.

Não copiar o HTML do mockup diretamente para produção.

## Telas implementadas

Cada rota carrega `data.title` (breadcrumb + título do diálogo de ajuda), `data.help` (texto do
diálogo de ajuda) e, quando a tela é conceitualmente filha de uma lista, `data.parent` (segundo
nível clicável do breadcrumb) — ver `app.routes.ts`.

| Rota | Componente | Tela |
|---|---|---|
| `/login` | `LoginComponent` | Login |
| `/dashboard` | `DashboardComponent` | Visão geral |
| `/lancamentos/conta` | `AccountTransactionsComponent` | Lançamentos da conta |
| `/lancamentos/cartao` | `CardTransactionsComponent` | Lançamentos do cartão |
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
| `/membros` | `GrupoMembersComponent` | Membros da família |
| `/registrar` | `RegisterComponent` | Criar conta (fora do shell, sem autenticação) |
| `/esqueci-senha` | `ForgotPasswordComponent` | Pedir link de redefinição de senha (fora do shell, sem autenticação) |
| `/redefinir-senha` | `ResetPasswordComponent` | Definir nova senha a partir do link recebido por e-mail (fora do shell, sem autenticação) |
| `/confirmar-email` | `ConfirmEmailComponent` | Confirma o e-mail automaticamente ao abrir o link recebido no cadastro (fora do shell, sem autenticação) |
| `/trocar-senha-inicial` | `ChangePasswordComponent` | Troca obrigatória da senha temporária no primeiro login (fora do menu — `authGuard` redireciona pra cá enquanto `MustChangePassword` estiver ativo) |
| `/admin/grupos` | `AdminGruposComponent` | Grupos (Administrator) |
| `/admin/perfis-acesso` | `AdminAccessProfilesComponent` | Perfis de acesso (Administrator) |
| `/admin/grupos/:id/membros` | `GrupoMembersComponent` (reaproveitado) | Membros do grupo (Administrator) |
| `/ajuda` | `HelpPageComponent` | Ajuda geral — todas as telas |

`/recorrencia/:id` é alcançável tanto por `/recorrencia` (lista) quanto por uma linha de
`/lancamentos` ("Editar recorrência") — por isso o botão Voltar dessa tela usa
`Location.back()` em vez de um destino fixo.
