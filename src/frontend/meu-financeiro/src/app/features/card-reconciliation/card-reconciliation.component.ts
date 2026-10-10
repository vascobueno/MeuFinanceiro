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
import { CardService } from '../../core/services/card.service';
import { CardReconciliationItem, ImportCardInvoiceRequest, ImportLineError, InvoiceCardHeader, ParsedCardInvoice } from '../../core/models/reconciliation.model';
import { businessRuleOf } from '../../core/services/problem.util';
import { CreditCard } from '../../core/models/card.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { IssuedCardService } from '../../core/services/issued-card.service';
import { IssuedCardKind } from '../../core/models/issued-card.model';
import { CardPaymentService } from '../../core/services/card-payment.service';
import { Tooltip } from 'primeng/tooltip';

/** docs/08-conciliacao-cartoes.md — entrada inicial: PDF da fatura (normalizado aqui manualmente). */
@Component({
  selector: 'mf-card-reconciliation',
  standalone: true,
  imports: [
    Tooltip,
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
  templateUrl: './card-reconciliation.component.html',
  styleUrl: './card-reconciliation.component.scss',
})
export class CardReconciliationComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<CardReconciliationItem[]>([]);
  readonly cards = signal<CreditCard[]>([]);
  readonly cardFilterOptions = computed(() => [{ id: null, name: 'Todos' }, ...this.cards()]);

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

  readonly statusOptions = [
    { label: 'Todos', value: '' },
    { label: 'Conciliado', value: 'Matched' },
    { label: 'Possível match', value: 'PossibleMatch' },
    { label: 'Sem correspondência', value: 'Unmatched' },
  ];

  selectedCardId: string | null = null;
  selectedStatus = '';

  readonly showImportForm = signal(false);
  importCardId: string | null = null;
  // Date (não string) por ser ligado a [(ngModel)] de p-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  closingDate: Date | null = null;
  dueDate: Date | null = null;
  totalAmount: number | null = null;
  /** Saldo da fatura anterior: a fatura fecha como saldo anterior + itens = total. */
  previousBalance: number | null = null;
  /** Cartões dos cabeçalhos da última leitura. */
  readonly parsedCards = signal<InvoiceCardHeader[]>([]);
  /** Finais que o grupo não tem, com o tipo escolhido para cadastrar. */
  readonly unknownCards = signal<{ header: InvoiceCardHeader; kind: IssuedCardKind }[]>([]);
  readonly unknownError = signal<string | null>(null);
  readonly detectedNote = signal<string | null>(null);
  private pendingParsed: ParsedCardInvoice | null = null;
  /** Fechamentos que a fatura imprime e que diferem do calendário do cartão. */
  readonly closingOffers = signal<{ cardId: string; reference: string; date: string; current: string; label: string }[]>([]);
  /** Uma linha por item: data;descrição;parcela;valor */
  itemsRaw = '';
  readonly importing = signal(false);
  readonly importResult = signal<string | null>(null);
  readonly parseErrors = signal<ImportLineError[]>([]);
  readonly unbalancedMessage = signal<string | null>(null);
  private pendingImport: ImportCardInvoiceRequest | null = null;

  constructor(
    private readonly cardPaymentService: CardPaymentService,
    private readonly issuedCardService: IssuedCardService,
    private readonly reconciliationService: ReconciliationService,
    private readonly cardService: CardService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.cards.set(r.items);
      if (r.items.length > 0) this.importCardId = r.items[0].id;
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reconciliationService.getCardReconciliations(this.selectedCardId ?? undefined, this.selectedStatus || undefined).subscribe({
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

  /** Leitura do texto pelo servidor — ver o comentário equivalente na conciliação bancária. */
  submitImport(): void {
    this.importResult.set(null);
    this.parseErrors.set([]);
    this.unbalancedMessage.set(null);

    const closingDate = this.toIsoDate(this.closingDate);
    const dueDate = this.toIsoDate(this.dueDate);
    // O total pode vir do VALOR TOTAL do texto; só é exigido depois da leitura.
    if (!this.importCardId || !closingDate || !dueDate) {
      this.importResult.set('Preencha cartão, fechamento e vencimento — o fechamento dá o ano às datas dd/MM da fatura.');
      return;
    }
    if (!this.itemsRaw.trim()) {
      this.importResult.set('Cole os itens da fatura.');
      return;
    }

    this.importing.set(true);
    this.unknownCards.set([]);
    this.parsedCards.set([]);
    this.reconciliationService.parseCardInvoice({ text: this.itemsRaw, closingDate }).subscribe({
      next: (parsed) => {
        if (parsed.errors.length > 0) {
          this.importing.set(false);
          this.parseErrors.set(parsed.errors);
          return;
        }
        if (parsed.items.length === 0) {
          this.importing.set(false);
          this.importResult.set('Nenhum item encontrado no texto colado.');
          return;
        }

        // Os finais dizem de que grupo é a fatura: se não for o escolhido, troca e avisa.
        this.detectedNote.set(null);
        if (parsed.detectedCreditCardId && parsed.detectedCreditCardId !== this.importCardId) {
          this.importCardId = parsed.detectedCreditCardId;
          this.detectedNote.set(`Fatura reconhecida como de ${parsed.detectedCreditCardName} pelos finais dos cartões.`);
        }
        if (this.totalAmount == null && parsed.totalFromText != null) this.totalAmount = parsed.totalFromText;
        if (this.totalAmount == null) {
          this.importing.set(false);
          this.importResult.set('Informe o total da fatura — o texto não trouxe um VALOR TOTAL só.');
          return;
        }

        const cards = parsed.cards ?? [];
        this.parsedCards.set(cards);
        this.pendingParsed = parsed;
        const unknown = cards.filter((c) => !c.issuedCardId || c.creditCardId !== this.importCardId);
        if (unknown.length > 0) {
          // Pausa: finais que o grupo não tem. A tela oferece cadastrá-los antes de importar.
          this.importing.set(false);
          this.unknownCards.set(unknown.map((c) => ({ header: c, kind: c.isVirtual ? IssuedCardKind.Online : IssuedCardKind.Adicional })));
          return;
        }
        this.continueImport();
      },
      error: () => {
        this.importing.set(false);
        this.importResult.set('Não foi possível ler o texto colado.');
      },
    });
  }

  /**
   * Importa o que foi lido. Itens de finais não cadastrados entram com o final guardado e sem
   * cartão ligado; os cadastrados aqui antes de continuar são ligados na importação.
   */
  continueImport(): void {
    const parsed = this.pendingParsed;
    const closingDate = this.toIsoDate(this.closingDate);
    const dueDate = this.toIsoDate(this.dueDate);
    if (!parsed || !this.importCardId || !closingDate || !dueDate || this.totalAmount == null) return;
    this.unknownCards.set([]);
    this.sendImport({
      creditCardId: this.importCardId,
      closingDate,
      dueDate,
      totalAmount: this.totalAmount,
      previousBalance: this.previousBalance ?? 0,
      items: parsed.items,
    });
  }

  /**
   * A fatura diz quando fechou (e às vezes quando fecha a próxima). Se o calendário do cartão
   * calcula outra data para aquele mês, oferece ajustar só aquele ciclo — a operadora antecipa ou
   * adia o fechamento em feriados e fins de semana.
   */
  private offerClosingAdjustments(cardId: string): void {
    this.closingOffers.set([]);
    const parsed = this.pendingParsed;
    if (!parsed) return;
    const wanted = [
      { date: parsed.closingDateFromText, label: 'desta fatura' },
      { date: parsed.nextClosingDateFromText, label: 'da próxima fatura' },
    ].filter((w): w is { date: string; label: string } => !!w.date);

    for (const w of wanted) {
      // Pelo mês: o ciclo de setembro é o que fecha em setembro, mesmo que a fatura diga dia 3 e o
      // cadastro diga dia 2 (pela data em si, o dia 3 cairia no ciclo de outubro).
      const reference = w.date.slice(0, 8) + '01';
      this.cardPaymentService.getCycle(cardId, reference).subscribe({
        next: (cycle) => {
          if (cycle.closingDate === w.date) return;
          this.closingOffers.set([
            ...this.closingOffers(),
            { cardId, reference: cycle.reference, date: w.date, current: cycle.closingDate, label: w.label },
          ]);
        },
      });
    }
  }

  applyClosingOffer(offer: { cardId: string; reference: string; date: string }): void {
    this.cardPaymentService.setClosing(offer.cardId, offer.reference, offer.date).subscribe({
      next: () => {
        this.closingOffers.set(this.closingOffers().filter((o) => o !== offer));
        this.importResult.set(`${this.importResult() ?? ''} Fechamento ajustado para ${this.formatIso(offer.date)}.`.trim());
      },
      error: (err) => this.unknownError.set(businessRuleOf(err)?.message ?? 'Não foi possível ajustar o fechamento.'),
    });
  }

  dismissClosingOffer(offer: object): void {
    this.closingOffers.set(this.closingOffers().filter((o) => o !== offer));
  }

  formatIso(iso: string): string {
    const [y, m, d] = iso.split('-');
    return `${d}/${m}/${y}`;
  }

  /** "Lançar" vale para item sem conciliação aceita, que não seja pagamento nem de valor zero. */
  canCreateFromItem(item: CardReconciliationItem): boolean {
    return !item.accepted && item.status !== 'Ignored' && item.itemKind !== 'Payment' && item.itemAmount !== 0;
  }

  createFromItem(item: CardReconciliationItem): void {
    this.reconciliationService.createTransactionFromCardItem(item.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(businessRuleOf(err)?.message ?? 'Não foi possível lançar o item.'),
    });
  }

  /** Cadastra no grupo da importação o cartão de um final que a fatura trouxe e o grupo não tinha. */
  addUnknownCard(entry: { header: InvoiceCardHeader; kind: IssuedCardKind }): void {
    if (!this.importCardId) return;
    this.unknownError.set(null);
    this.issuedCardService
      .create(this.importCardId, {
        kind: entry.kind,
        lastFour: entry.header.lastFour,
        firstDigits: entry.header.firstDigits,
        holderName: entry.header.holderName,
      })
      .subscribe({
        next: () => {
          const rest = this.unknownCards().filter((u) => u !== entry);
          // O chip do cartão deixa de aparecer como desconhecido.
          this.parsedCards.set(
            this.parsedCards().map((c) =>
              c.lastFour === entry.header.lastFour ? { ...c, issuedCardId: 'novo', creditCardId: this.importCardId } : c,
            ),
          );
          this.unknownCards.set(rest);
          // Cadastrado o último que faltava, a importação segue sozinha.
          if (rest.length === 0) this.continueImport();
        },
        error: (err) => this.unknownError.set(businessRuleOf(err)?.message ?? 'Não foi possível cadastrar o cartão.'),
      });
  }

  groupName(id: string | null): string {
    return this.cards().find((c) => c.id === id)?.name ?? '';
  }

  readonly kindOptions = [
    { label: 'Titular', value: IssuedCardKind.Titular },
    { label: 'Adicional', value: IssuedCardKind.Adicional },
    { label: 'Virtual', value: IssuedCardKind.Online },
  ];

  itemKindLabel(kind: string | null | undefined): string {
    switch (kind) {
      case 'Installment': return 'Parcela';
      case 'Payment': return 'Pagamento';
      case 'Credit': return 'Crédito';
      case 'Fee': return 'Tarifa';
      case 'Tax': return 'IOF';
      case 'Expense': return 'Compra';
      default: return '';
    }
  }

  /** "US$ 21,22 × 5,4970" (+ "calculada") e, se houver, "EUR 19,50". */
  foreignLine(item: CardReconciliationItem): string {
    const parts: string[] = [];
    if (item.itemForeignAmount != null) {
      const symbol = item.itemForeignCurrency === 'USD' ? 'US$' : item.itemForeignCurrency ?? '';
      const amount = item.itemForeignAmount.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      let line = `${symbol} ${amount}`;
      if (item.itemExchangeRate != null) {
        const rate = item.itemExchangeRate.toLocaleString('pt-BR', { minimumFractionDigits: 4, maximumFractionDigits: 6 });
        line += ` × ${rate}${item.itemExchangeRateEstimated ? ' (calculada)' : ''}`;
      }
      parts.push(line);
    }
    if (item.itemOriginalAmount != null && item.itemOriginalCurrency) {
      const original = item.itemOriginalAmount.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      parts.push(`origem ${item.itemOriginalCurrency} ${original}`);
    }
    return parts.join(' · ');
  }

  confirmUnbalanced(): void {
    if (this.pendingImport) this.sendImport({ ...this.pendingImport, confirmUnbalanced: true });
  }

  cancelUnbalanced(): void {
    this.pendingImport = null;
    this.unbalancedMessage.set(null);
  }

  private sendImport(request: ImportCardInvoiceRequest): void {
    this.importing.set(true);
    this.unbalancedMessage.set(null);

    this.reconciliationService.importCardInvoice(request).subscribe({
      next: (invoice) => {
        this.importing.set(false);
        this.pendingImport = null;
        this.importResult.set(
          invoice.totalValidated
            ? `Fatura importada (${invoice.itemCount} itens) e o total confere. ✅`
            : `Fatura importada (${invoice.itemCount} itens) sem fechar com o total, como confirmado. ⚠️`,
        );
        this.itemsRaw = '';
        this.load();
        this.offerClosingAdjustments(request.creditCardId);
      },
      error: (err) => {
        this.importing.set(false);
        const rule = businessRuleOf(err);
        if (rule?.rule === 'fatura-nao-fecha') {
          this.pendingImport = request;
          this.unbalancedMessage.set(rule.message);
          return;
        }
        this.importResult.set(rule?.message ?? 'Não foi possível importar a fatura.');
      },
    });
  }

  accept(item: CardReconciliationItem): void {
    this.reconciliationService.acceptCardReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  undo(item: CardReconciliationItem): void {
    this.reconciliationService.undoCardReconciliation(item.id).subscribe({ next: () => this.load() });
  }

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
      case 'Ignored':
        return 'Ignorado';
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

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
