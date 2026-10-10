import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { Textarea } from 'primeng/textarea';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { AuthService } from '../../core/services/auth.service';
import { ReconciliationService } from '../../core/services/reconciliation.service';
import { AccountService } from '../../core/services/account.service';
import { BankReconciliationItem, ImportBankStatementRequest, ImportLineError, ParsedOfx } from '../../core/models/reconciliation.model';
import { businessRuleOf } from '../../core/services/problem.util';
import { Account } from '../../core/models/account.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { statementItemTypeLabel } from '../../core/services/statement-item-type.util';

/**
 * docs/09-conciliacao-conta-corrente.md: importa extrato normalizado (parser de
 * PDF/OFX fica fora do escopo desta versão — aceita-se lançamento manual dos itens ou um
 * CSV simples colado no campo de texto) e mostra candidatos de conciliação por score.
 */
@Component({
  selector: 'mf-bank-reconciliation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MfIconComponent,
    ButtonDirective,
    InputText,
    Select,
    DatePicker,
    Textarea,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    StatCardComponent,
  ],
  templateUrl: './bank-reconciliation.component.html',
  styleUrl: './bank-reconciliation.component.scss',
})
export class BankReconciliationComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<BankReconciliationItem[]>([]);
  readonly accounts = signal<Account[]>([]);
  readonly accountFilterOptions = computed(() => [{ id: null, name: 'Todas' }, ...this.accounts()]);

  // KPIs calculados a partir dos itens já carregados na tela (sem endpoint de agregação novo).
  get matchedCount(): number {
    return this.items().filter((i) => i.status === 'Matched' || i.status === 'ManuallyMatched').length;
  }
  get possibleCount(): number {
    return this.items().filter((i) => i.status === 'PossibleMatch').length;
  }
  get unmatchedCount(): number {
    return this.items().filter((i) => i.status === 'Unmatched').length;
  }
  get matchedPercent(): number {
    return this.items().length === 0 ? 0 : Math.round((this.matchedCount / this.items().length) * 100);
  }

  readonly statusOptions = [
    { label: 'Todos', value: '' },
    { label: 'Conciliado', value: 'Matched' },
    { label: 'Possível match', value: 'PossibleMatch' },
    { label: 'Sem correspondência', value: 'Unmatched' },
  ];

  selectedAccountId: string | null = null;
  selectedStatus = '';

  readonly showImportForm = signal(false);
  importAccountId: string | null = null;
  // Date (não string) por ser ligado a [(ngModel)] de p-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  referenceStart: Date | null = null;
  referenceEnd: Date | null = null;
  openingBalance: number | null = null;
  closingBalance: number | null = null;
  /** Texto colado do extrato — o servidor lê (formato do banco ou data;descrição;valor). */
  itemsRaw = '';
  readonly importing = signal(false);
  readonly importResult = signal<string | null>(null);
  /** Linhas que o servidor não conseguiu ler, com o motivo. */
  readonly parseErrors = signal<ImportLineError[]>([]);
  /** Extrato que não fecha: a mensagem com a diferença, à espera de confirmação. */
  readonly unbalancedMessage = signal<string | null>(null);
  private pendingImport: ImportBankStatementRequest | null = null;
  /** OFX lido e ainda não importado: os movimentos vêm daqui em vez do texto colado. */
  readonly ofx = signal<ParsedOfx | null>(null);
  ofxFileName = '';

  constructor(
    private readonly reconciliationService: ReconciliationService,
    private readonly accountService: AccountService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.accounts.set(r.items);
      if (r.items.length > 0) this.importAccountId = r.items[0].id;
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reconciliationService.getBankReconciliations(this.selectedAccountId ?? undefined, this.selectedStatus || undefined).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as conciliações.');
        this.loading.set(false);
      },
    });
  }

  /**
   * A leitura do texto é do servidor: antes era um `Number(valor)` aqui, e qualquer valor no
   * formato do extrato (`1.288,46-`) virava NaN e a importação era recusada sem explicação.
   */
  /**
   * O servidor lê o OFX e reconhece a conta pelos dados bancários do cadastro. A tela só preenche
   * o formulário: conta, período e saldos ficam editáveis, e "Importar" manda os movimentos lidos.
   */
  onOfxSelected(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.importResult.set(null);
    this.parseErrors.set([]);
    this.unbalancedMessage.set(null);
    this.importing.set(true);
    this.reconciliationService.parseOfx(file).subscribe({
      next: (parsed) => {
        this.importing.set(false);
        if (parsed.errors.length > 0) {
          this.parseErrors.set(parsed.errors);
          return;
        }
        this.ofx.set(parsed);
        this.ofxFileName = file.name;
        if (parsed.referenceStart) this.referenceStart = this.fromIsoDate(parsed.referenceStart);
        if (parsed.referenceEnd) this.referenceEnd = this.fromIsoDate(parsed.referenceEnd);
        this.openingBalance = parsed.openingBalance;
        this.closingBalance = parsed.closingBalance;

        if (parsed.accountId) {
          this.importAccountId = parsed.accountId;
          this.importResult.set(
            `Conta ${parsed.accountName} reconhecida (${parsed.accountFromFile}). O saldo inicial é o final menos os movimentos. Confira e importe.`,
          );
        } else {
          this.importAccountId = null;
          this.importResult.set(
            parsed.matchingAccounts > 1
              ? `Mais de uma conta cadastrada casa com ${parsed.accountFromFile}. Escolha a conta e importe.`
              : `Nenhuma conta cadastrada com ${parsed.accountFromFile}. Escolha a conta — ou cadastre banco, agência e conta nela para o próximo arquivo ser reconhecido.`,
          );
        }
      },
      error: (err) => {
        this.importing.set(false);
        this.importResult.set(businessRuleOf(err)?.message ?? 'Não foi possível ler o arquivo OFX.');
      },
    });
  }

  clearOfx(): void {
    this.ofx.set(null);
    this.ofxFileName = '';
    this.importResult.set(null);
  }

  submitImport(): void {
    this.importResult.set(null);
    this.parseErrors.set([]);
    this.unbalancedMessage.set(null);

    const ofx = this.ofx();
    if (ofx) {
      const start = this.toIsoDate(this.referenceStart);
      const end = this.toIsoDate(this.referenceEnd);
      if (!this.importAccountId || !start || !end || this.openingBalance == null || this.closingBalance == null) {
        this.importResult.set('Preencha conta, período e saldos para importar o OFX.');
        return;
      }
      this.sendImport({
        accountId: this.importAccountId,
        referenceStart: start,
        referenceEnd: end,
        openingBalance: this.openingBalance,
        closingBalance: this.closingBalance,
        originalFileName: this.ofxFileName,
        items: ofx.items,
      });
      return;
    }

    const referenceStart = this.toIsoDate(this.referenceStart);
    const referenceEnd = this.toIsoDate(this.referenceEnd);
    if (!this.importAccountId || !referenceStart || !referenceEnd) {
      this.importResult.set('Preencha conta, início e fim do período — o período dá o ano às datas dd/MM do extrato.');
      return;
    }
    if (!this.itemsRaw.trim()) {
      this.importResult.set('Cole os movimentos do extrato.');
      return;
    }

    const accountId = this.importAccountId;
    this.importing.set(true);
    this.reconciliationService.parseBankStatement({ text: this.itemsRaw, referenceStart, referenceEnd }).subscribe({
      next: (parsed) => {
        if (parsed.errors.length > 0) {
          this.importing.set(false);
          this.parseErrors.set(parsed.errors);
          return;
        }
        if (parsed.items.length === 0) {
          this.importing.set(false);
          this.importResult.set('Nenhum movimento encontrado no texto colado.');
          return;
        }
        // As linhas "SALDO EM dd/MM" do próprio extrato preenchem os saldos que estiverem vazios.
        if (this.openingBalance == null && parsed.openingBalance != null) this.openingBalance = parsed.openingBalance;
        if (this.closingBalance == null && parsed.closingBalance != null) this.closingBalance = parsed.closingBalance;
        if (this.openingBalance == null || this.closingBalance == null) {
          this.importing.set(false);
          this.importResult.set(`${parsed.items.length} movimentos lidos. Informe o saldo inicial e o final para importar.`);
          return;
        }

        this.sendImport({
          accountId,
          referenceStart,
          referenceEnd,
          openingBalance: this.openingBalance,
          closingBalance: this.closingBalance,
          items: parsed.items,
        });
      },
      error: () => {
        this.importing.set(false);
        this.importResult.set('Não foi possível ler o texto colado.');
      },
    });
  }

  /** "Importar mesmo assim" depois do aviso de que o extrato não fecha. */
  confirmUnbalanced(): void {
    if (this.pendingImport) this.sendImport({ ...this.pendingImport, confirmUnbalanced: true });
  }

  cancelUnbalanced(): void {
    this.pendingImport = null;
    this.unbalancedMessage.set(null);
  }

  private sendImport(request: ImportBankStatementRequest): void {
    this.importing.set(true);
    this.unbalancedMessage.set(null);

    this.reconciliationService.importBankStatement(request).subscribe({
      next: (statement) => {
        this.importing.set(false);
        this.pendingImport = null;
        const skipped = statement.skippedDuplicates
          ? ` ${statement.skippedDuplicates} movimentos já importados ficaram de fora.`
          : '';
        this.importResult.set(
          (statement.balanceValidated
            ? `Extrato importado (${statement.itemCount} ${statement.itemCount === 1 ? 'item' : 'itens'}). Saldo inicial + movimentos fecha com o saldo final. ✅`
            : `Extrato importado (${statement.itemCount} ${statement.itemCount === 1 ? 'item' : 'itens'}) sem fechar com o saldo final, como confirmado. ⚠️`) + skipped,
        );
        this.itemsRaw = '';
        this.ofx.set(null);
        this.ofxFileName = '';
        this.load();
      },
      error: (err) => {
        this.importing.set(false);
        const rule = businessRuleOf(err);
        if (rule?.rule === 'extrato-nao-fecha') {
          this.pendingImport = request;
          this.unbalancedMessage.set(rule.message);
          return;
        }
        // Duplicado e demais regras já vêm com a mensagem em português.
        this.importResult.set(rule?.message ?? 'Não foi possível importar o extrato.');
      },
    });
  }

  accept(item: BankReconciliationItem): void {
    this.reconciliationService.acceptBankReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  undo(item: BankReconciliationItem): void {
    this.reconciliationService.undoBankReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  readonly typeLabel = statementItemTypeLabel;

  statusLabel(status: string): string {
    switch (status) {
      case 'Matched':
        return 'Conciliado';
      case 'PossibleMatch':
        return 'Possível match';
      case 'ManuallyMatched':
        return 'Conciliado manualmente';
      case 'Unmatched':
        return 'Sem correspondência';
      default:
        return status;
    }
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Matched':
      case 'ManuallyMatched':
        return 'mf-badge-ok';
      case 'PossibleMatch':
        return 'mf-badge-warn';
      default:
        return 'mf-badge-neutral';
    }
  }

  /** "2023-01-31" como data local — new Date("2023-01-31") seria meia-noite UTC, dia anterior no Brasil. */
  private fromIsoDate(value: string): Date {
    const [y, mo, d] = value.split('-').map(Number);
    return new Date(y, mo - 1, d);
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
