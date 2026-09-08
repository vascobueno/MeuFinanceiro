import { ChangeDetectorRef, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { ButtonDirective } from 'primeng/button';
import { Drawer } from 'primeng/drawer';
import { Tooltip } from 'primeng/tooltip';
import { DialogService } from 'primeng/dynamicdialog';
import { filter, map } from 'rxjs';
import { AuthService } from '../core/services/auth.service';
import { HelpDialogComponent } from '../shared/components/help-dialog.component';
import { MfIconComponent } from '../shared/icons/mf-icon.component';
import { routes } from '../app.routes';

interface BreadcrumbParent {
  title: string;
  path: string;
}

/**
 * Casca da aplicação (docs/migracao-primeng/00-plano.md, UI-02.1 — migrado de
 * mat-sidenav-container/mat-toolbar/mat-nav-list pra PrimeNG + Tailwind). Desktop: sidebar
 * fixa (aside comum, empurra o conteúdo — não precisa de overlay). Mobile: `p-drawer`
 * (overlay de verdade, com máscara), aberto pelo hambúrguer da topbar. Ícones de navegação
 * custom via `<mf-icon name="mf-*">` (Fase 6, UI-06.2 — troca do `MatIconRegistry`).
 */
@Component({
  selector: 'mf-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, MfIconComponent, ButtonDirective, Drawer, Tooltip],
  providers: [DialogService],
  templateUrl: './shell.component.html',
})
export class ShellComponent implements OnInit {
  private readonly breakpointObserver = inject(BreakpointObserver);
  private readonly dialogService = inject(DialogService);

  private static readonly COLLAPSED_STORAGE_KEY = 'mf-sidebar-collapsed';
  private static readonly SECTIONS_STORAGE_KEY = 'mf-sidebar-sections';

  readonly pageTitle = signal('MeuFinanceiro');
  readonly pageHelp = signal<string | null>(null);
  readonly breadcrumbParent = signal<BreadcrumbParent | null>(null);
  // Só relevante no desktop (sidebar fixa); no mobile o drawer sempre abre "cheio" via
  // hambúrguer. Persistido em localStorage pra lembrar a preferência entre sessões.
  readonly collapsed = signal(localStorage.getItem(ShellComponent.COLLAPSED_STORAGE_KEY) === '1');
  // Cada seção do menu (exceto Principal) é recolhível — só a seção da página atual abre
  // sozinha por padrão; o resto do estado (o que o usuário abriu/fechou manualmente) persiste.
  readonly expandedSections = signal<Record<string, boolean>>(this.loadExpandedSections());
  // Estado do p-drawer mobile (substitui o ViewChild MatSidenav de antes).
  readonly mobileDrawerOpen = signal(false);

  readonly isMobile = toSignal(
    this.breakpointObserver.observe('(max-width: 768px)').pipe(map((result) => result.matches)),
    { initialValue: false },
  );
  // No mobile o drawer é overlay (some/aparece inteiro) — recolher não se aplica lá.
  readonly isCollapsedDesktop = computed(() => this.collapsed() && !this.isMobile());

  constructor(
    readonly authService: AuthService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.updateTitle();

    // Evento do Router (não de template) — sob zoneless, precisa de markForCheck() explícito
    // para a view ser reavaliada (docs/22, mesmo padrão do AutocompleteComponent/MoneyInputComponent).
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      this.updateTitle();
      if (this.isMobile()) this.mobileDrawerOpen.set(false);
      this.cdr.markForCheck();
    });
  }

  private updateTitle(): void {
    const data = this.route.firstChild?.snapshot.data ?? {};
    this.pageTitle.set((data['title'] as string) ?? 'MeuFinanceiro');
    this.pageHelp.set((data['help'] as string) ?? null);
    this.breadcrumbParent.set((data['parent'] as BreadcrumbParent) ?? null);
    this.ensureActiveSectionExpanded(this.resolveSection(data));
  }

  /** `data.section` direto, ou (rota filha, ex. /lancamentos/novo) a seção da rota-pai via `data.parent.path`. */
  private resolveSection(data: Record<string, unknown>): string | undefined {
    if (data['section']) return data['section'] as string;

    const parent = data['parent'] as BreadcrumbParent | undefined;
    if (!parent) return undefined;

    const shellChildren = routes.find((r) => r.path === '')?.children ?? [];
    const parentPath = parent.path.replace(/^\//, '');
    return shellChildren.find((r) => r.path === parentPath)?.data?.['section'] as string | undefined;
  }

  /** Só garante que a seção da página atual fique visível — não mexe no que o usuário abriu/fechou manualmente. */
  private ensureActiveSectionExpanded(section: string | undefined): void {
    if (section && !this.isSectionExpanded(section)) {
      this.expandedSections.update((s) => ({ ...s, [section]: true }));
    }
  }

  isSectionExpanded(section: string): boolean {
    return this.expandedSections()[section] ?? false;
  }

  toggleSection(section: string): void {
    const next = { ...this.expandedSections(), [section]: !this.isSectionExpanded(section) };
    this.expandedSections.set(next);
    localStorage.setItem(ShellComponent.SECTIONS_STORAGE_KEY, JSON.stringify(next));
  }

  private loadExpandedSections(): Record<string, boolean> {
    try {
      const raw = localStorage.getItem(ShellComponent.SECTIONS_STORAGE_KEY);
      return raw ? (JSON.parse(raw) as Record<string, boolean>) : {};
    } catch {
      return {};
    }
  }

  toggleCollapsed(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem(ShellComponent.COLLAPSED_STORAGE_KEY, next ? '1' : '0');
  }

  openHelp(): void {
    this.dialogService.open(HelpDialogComponent, {
      header: this.pageTitle(),
      data: { body: this.pageHelp() ?? 'Sem ajuda disponível para esta tela.' },
      width: '420px',
      modal: true,
      // Ao contrário de `modal`/`closeOnEscape`, `closable` não tem valor padrão true no
      // DynamicDialogConfig — sem isso o "X" de fechar no cabeçalho simplesmente não
      // aparece (achado via QA visual).
      closable: true,
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
