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
