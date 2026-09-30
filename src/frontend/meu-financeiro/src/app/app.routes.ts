import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { fullAccessGuard } from './core/guards/full-access.guard';
import { canWriteGuard } from './core/guards/can-write.guard';

/// Organização de rotas conforme docs/03-arquitetura.md e docs/14-angular.md.
/// `data.title` alimenta o breadcrumb da topbar e o título do diálogo de ajuda (mf-shell).
/// `data.help` é o texto mostrado no diálogo de ajuda (ícone "?" da topbar).
/// `data.parent` (opcional) vira o segundo nível clicável do breadcrumb, para telas que são
/// conceitualmente "filhas" de uma lista (ex.: Novo lançamento é filha de Lançamentos).
/// `data.section` (só nas rotas de topo, sem `parent`) agrupa a tela numa seção do menu
/// lateral (ver shell.component.html) — usado por HelpPageComponent (/ajuda) pra montar a
/// ajuda geral com a mesma organização do menu, sem duplicar `data.help` em outro lugar.
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'registrar',
    loadComponent: () => import('./features/auth/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'esqueci-senha',
    loadComponent: () => import('./features/auth/forgot-password.component').then((m) => m.ForgotPasswordComponent),
  },
  {
    path: 'redefinir-senha',
    loadComponent: () => import('./features/auth/reset-password.component').then((m) => m.ResetPasswordComponent),
  },
  {
    path: 'confirmar-email',
    loadComponent: () => import('./features/auth/confirm-email.component').then((m) => m.ConfirmEmailComponent),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'trocar-senha-inicial',
        loadComponent: () =>
          import('./features/auth/change-password.component').then((m) => m.ChangePasswordComponent),
        data: { title: 'Trocar senha' },
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
        data: {
          title: 'Visão geral',
          section: 'Principal',
          help: 'Visão consolidada das suas finanças: saldo em contas, patrimônio total, evolução de caixa dos últimos meses e os próximos compromissos a vencer. Use como ponto de partida para saber rapidamente como está sua situação financeira.',
        },
      },
      // ORDEM IMPORTA: 'lancamentos/conta' e 'lancamentos/cartao' precisam vir ANTES de
      // 'lancamentos/:id', senão o :id captura "conta" e a tela de edição abre tentando carregar
      // um lançamento com esse id — falha silenciosa que parece bug de dados.
      { path: 'lancamentos', pathMatch: 'full', redirectTo: 'lancamentos/conta' },
      {
        path: 'lancamentos/conta',
        loadComponent: () =>
          import('./features/transactions/account-transactions.component').then((m) => m.AccountTransactionsComponent),
        data: {
          title: 'Lançamentos da conta',
          section: 'Financeiro',
          help: 'Movimentações das contas correntes, com o saldo da conta escolhida no topo. Filtre por período, categoria, tipo e status. Os pagamentos de fatura de cartão aparecem aqui como "Pagamento de cartão" — eles saem da conta, mas não contam como despesa, porque a compra no cartão já foi contada.',
        },
      },
      {
        path: 'lancamentos/cartao',
        loadComponent: () =>
          import('./features/transactions/card-transactions.component').then((m) => m.CardTransactionsComponent),
        data: {
          title: 'Lançamentos do cartão',
          section: 'Financeiro',
          help: 'Fatura do ciclo do cartão, calculada a partir dos seus lançamentos: total gasto, quanto já foi pago e quanto falta. Navegue entre os ciclos pelas setas. Use "Pagar fatura" para registrar o pagamento, escolhendo a conta de onde o dinheiro saiu ou marcando como pago por outra forma.',
        },
      },
      {
        path: 'lancamentos/novo',
        canActivate: [canWriteGuard],
        loadComponent: () =>
          import('./features/transactions/transaction-form.component').then((m) => m.TransactionFormComponent),
        data: {
          title: 'Novo lançamento',
          parent: { title: 'Lançamentos da conta', path: '/lancamentos/conta' },
          help: 'Registre um lançamento simples, um parcelamento ou uma recorrência. A descrição sugere lançamentos anteriores parecidos para agilizar o preenchimento, e a categoria ajuda a manter seus relatórios organizados.',
        },
      },
      {
        path: 'lancamentos/:id',
        canActivate: [canWriteGuard],
        loadComponent: () =>
          import('./features/transactions/transaction-form.component').then((m) => m.TransactionFormComponent),
        data: {
          title: 'Editar lançamento',
          parent: { title: 'Lançamentos da conta', path: '/lancamentos/conta' },
          help: 'Ajuste um lançamento já existente. Lançamentos já conciliados com o extrato bancário ou a fatura do cartão são bloqueados para edição automática — use as telas de Conciliação para tratar divergências nesses casos.',
        },
      },
      {
        path: 'contas',
        loadComponent: () => import('./features/accounts/account-list.component').then((m) => m.AccountListComponent),
        data: {
          title: 'Contas correntes',
          section: 'Financeiro',
          help: 'Cadastre e acompanhe suas contas correntes e poupanças, com o saldo atual de cada uma e o total consolidado (sem somar investimentos). Uma conta com lançamentos vinculados não pode ser excluída, apenas desativada.',
        },
      },
      {
        path: 'parcelamento',
        loadComponent: () =>
          import('./features/installments/installment-list.component').then((m) => m.InstallmentListComponent),
        data: {
          title: 'Parcelamentos',
          section: 'Financeiro',
          help: 'Lista os parcelamentos criados, com a origem, o total e quantas parcelas já foram conciliadas. Clique numa linha para ver parcela a parcela: número, vencimento, valor e status.',
        },
      },
      {
        path: 'parcelamento/novo',
        canActivate: [canWriteGuard],
        loadComponent: () =>
          import('./features/installments/installment-form.component').then((m) => m.InstallmentFormComponent),
        data: {
          title: 'Novo parcelamento',
          parent: { title: 'Parcelamentos', path: '/parcelamento' },
          help: 'Divida uma compra em parcelas informando o valor total (dividido pela quantidade) ou o valor de cada parcela (multiplicado pela quantidade). A soma das parcelas sempre fecha exatamente o valor principal, com o ajuste de arredondamento ficando na última parcela. Cada parcela vira um lançamento independente.',
        },
      },
      {
        path: 'recorrencia',
        loadComponent: () =>
          import('./features/recurring/recurring-list.component').then((m) => m.RecurringListComponent),
        data: {
          title: 'Recorrências',
          section: 'Financeiro',
          help: 'Lista os lançamentos recorrentes cadastrados (assinaturas, salário, aluguel etc.), com a frequência, o valor vigente e uma prévia das próximas ocorrências. Alterar o valor de uma recorrência cria uma nova versão a partir de uma data de vigência — lançamentos passados e conciliados nunca são reescritos.',
        },
      },
      {
        path: 'recorrencia/:id',
        canActivate: [canWriteGuard],
        loadComponent: () =>
          import('./features/recurring/recurring-form.component').then((m) => m.RecurringFormComponent),
        data: {
          title: 'Recorrência',
          parent: { title: 'Recorrências', path: '/recorrencia' },
          help: 'Altere o valor de uma recorrência a partir de uma data futura. A mudança vale só a partir da data de vigência escolhida; ocorrências anteriores, incluindo as já conciliadas, permanecem com o valor original.',
        },
      },
      {
        path: 'faturas',
        loadComponent: () => import('./features/invoices/invoice-list.component').then((m) => m.InvoiceListComponent),
        data: {
          title: 'Faturas',
          section: 'Financeiro',
          help: 'Acompanhe as faturas geradas para cada cartão, com período de fechamento, vencimento, valor total e status (aberta ou vencida). Use os filtros para localizar uma fatura específica por cartão, período ou faixa de valor.',
        },
      },
      {
        path: 'cartoes',
        loadComponent: () => import('./features/cards/card-list.component').then((m) => m.CardListComponent),
        data: {
          title: 'Cartões',
          section: 'Financeiro',
          help: 'Cadastre seus cartões de crédito com dia de fechamento, vencimento e limite. Os cards no topo mostram o limite total, as faturas em aberto e o limite disponível já descontando essas faturas.',
        },
      },
      {
        path: 'categorias',
        loadComponent: () =>
          import('./features/categories/category-list.component').then((m) => m.CategoryListComponent),
        data: {
          title: 'Categorias',
          parent: { title: 'Configurações', path: '/configuracoes' },
          help: 'Organize categorias e subcategorias usadas para classificar receitas e despesas nos lançamentos. Uma categoria já usada em algum lançamento não pode ser excluída.',
        },
      },
      {
        path: 'conciliacao-conta',
        loadComponent: () =>
          import('./features/bank-reconciliation/bank-reconciliation.component').then(
            (m) => m.BankReconciliationComponent,
          ),
        data: {
          title: 'Conciliação bancária',
          section: 'Conciliação',
          help: 'Importe um extrato bancário e confira automaticamente quais lançamentos já registrados batem com os itens do extrato, por valor e data aproximada. Aceite as sugestões corretas ou vincule manualmente os casos que o sistema não conseguiu casar sozinho.',
        },
      },
      {
        path: 'conciliacao-cartao',
        loadComponent: () =>
          import('./features/card-reconciliation/card-reconciliation.component').then(
            (m) => m.CardReconciliationComponent,
          ),
        data: {
          title: 'Conciliação de cartão',
          section: 'Conciliação',
          help: 'Importe uma fatura de cartão e confira quais lançamentos já registrados correspondem a cada item da fatura. Funciona como a conciliação bancária, mas comparando com os itens da fatura em vez do extrato.',
        },
      },
      {
        path: 'investimentos',
        loadComponent: () => import('./features/investments/investments.component').then((m) => m.InvestmentsComponent),
        data: {
          title: 'Investimentos',
          section: 'Patrimônio',
          help: 'Registre a posição de investimentos separadamente das contas correntes — o saldo aqui nunca é somado automaticamente ao saldo das contas, só ao patrimônio total. Não há projeção de rentabilidade futura, apenas a posição informada.',
        },
      },
      {
        path: 'projecao',
        loadComponent: () => import('./features/projection/projection.component').then((m) => m.ProjectionComponent),
        data: {
          title: 'Projeção financeira',
          section: 'Patrimônio',
          help: 'Projete o saldo das contas e o patrimônio total até uma data futura, somando lançamentos futuros já registrados (parcelas, primeira ocorrência de recorrências) e as ocorrências futuras de recorrências ainda não materializadas. Um alerta indica se o saldo projetado fica negativo em algum ponto do caminho.',
        },
      },
      {
        path: 'relatorios/projecao-receitas',
        loadComponent: () =>
          import('./features/reports/income-projection-report.component').then((m) => m.IncomeProjectionReportComponent),
        data: {
          title: 'Projeção de receitas',
          section: 'Relatórios',
          help: 'Lista as receitas previstas para os próximos 90 dias (lançamentos futuros já registrados + ocorrências futuras de recorrências), com botão para imprimir ou salvar como PDF.',
        },
      },
      {
        path: 'relatorios/gastos-recorrentes',
        loadComponent: () =>
          import('./features/reports/recurring-expenses-report.component').then((m) => m.RecurringExpensesReportComponent),
        data: {
          title: 'Gastos recorrentes',
          section: 'Relatórios',
          help: 'Lista os gastos recorrentes ativos, com frequência, valor atual e a próxima ocorrência, com botão para imprimir ou salvar como PDF.',
        },
      },
      {
        path: 'relatorios/auditoria',
        loadComponent: () => import('./features/reports/audit-log.component').then((m) => m.AuditLogComponent),
        data: {
          title: 'Auditoria',
          section: 'Relatórios',
          help: 'Acessos (login) e alterações de dados do seu grupo, filtráveis por período.',
        },
      },
      {
        path: 'configuracoes',
        canActivate: [fullAccessGuard],
        loadComponent: () => import('./features/settings/settings.component').then((m) => m.SettingsComponent),
        data: {
          title: 'Configurações',
          section: 'Sistema',
          help: 'Gerencie categorias e conduza a migração inicial de dados para começar a usar o sistema com seu histórico existente.',
        },
      },
      {
        path: 'membros',
        canActivate: [fullAccessGuard],
        loadComponent: () =>
          import('./features/grupo/grupo-members.component').then((m) => m.GrupoMembersComponent),
        data: {
          title: 'Membros',
          parent: { title: 'Configurações', path: '/configuracoes' },
          help: 'Veja e gerencie quem mais tem acesso aos seus dados financeiros. Só o Owner do grupo pode adicionar ou remover membros, ou mudar o perfil de acesso de alguém.',
        },
      },
      {
        path: 'admin/grupos',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/admin/admin-grupos.component').then((m) => m.AdminGruposComponent),
        data: {
          title: 'Grupos',
          section: 'Administração',
          help: 'Visão de administrador: todos os grupos do sistema, com seus donos e membros.',
        },
      },
      {
        path: 'admin/perfis-acesso',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/admin/admin-access-profiles.component').then((m) => m.AdminAccessProfilesComponent),
        data: {
          title: 'Perfis de acesso',
          section: 'Administração',
          help: 'Visão cruzada de todos os membros de todos os grupos — ajuste o perfil de acesso (Completo/Editor/Visualizador) de qualquer um sem precisar entrar grupo por grupo.',
        },
      },
      {
        path: 'admin/grupos/:id/membros',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/grupo/grupo-members.component').then((m) => m.GrupoMembersComponent),
        data: {
          title: 'Membros do grupo',
          parent: { title: 'Grupos', path: '/admin/grupos' },
          help: 'Membros deste grupo — como administrador, você pode adicionar ou remover qualquer um deles.',
        },
      },
      {
        path: 'ajuda',
        loadComponent: () => import('./features/help/help-page.component').then((m) => m.HelpPageComponent),
        data: {
          title: 'Ajuda',
          help: 'Guia rápido de todas as telas do sistema, organizado como o menu lateral — reaproveita o mesmo texto de ajuda mostrado no "?" de cada tela.',
        },
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
