import { Component, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'primeng/button';
import { Accordion, AccordionPanel, AccordionHeader, AccordionContent } from 'primeng/accordion';
import { AuthService } from '../../core/services/auth.service';
import { routes } from '../../app.routes';

interface HelpEntry {
  path: string;
  title: string;
  help: string;
  children: HelpEntry[];
}

interface HelpSection {
  name: string;
  entries: HelpEntry[];
  // Todos os paths das entries — alimenta [value] do p-accordion pra manter tudo expandido
  // por padrão (mesmo comportamento do mat-accordion multi + [expanded]="true" de antes).
  entryPaths: string[];
}

const SECTION_ORDER = ['Principal', 'Financeiro', 'Conciliação', 'Patrimônio', 'Relatórios', 'Sistema', 'Administração'];

/**
 * Ajuda geral: mostra todas as telas do sistema agrupadas como o menu lateral, reaproveitando
 * o mesmo `data.title`/`data.help`/`data.parent`/`data.section` já usado pelo diálogo "?" por
 * tela (shell.component.ts) — uma fonte só de texto de ajuda, nunca duas descoladas.
 */
@Component({
  selector: 'mf-help-page',
  standalone: true,
  imports: [CommonModule, ButtonDirective, Accordion, AccordionPanel, AccordionHeader, AccordionContent],
  templateUrl: './help-page.component.html',
  styleUrl: './help-page.component.scss',
})
export class HelpPageComponent {
  readonly sections = computed<HelpSection[]>(() => this.buildSections());

  constructor(private readonly authService: AuthService) {}

  print(): void {
    window.print();
  }

  private buildSections(): HelpSection[] {
    const shellChildren = routes.find((r) => r.path === '')?.children ?? [];

    const topLevel = shellChildren.filter((r) => !!r.data?.['section']);
    const nested = shellChildren.filter((r) => !!r.data?.['parent']);

    const toEntry = (route: (typeof shellChildren)[number]): HelpEntry => {
      const path = '/' + (route.path ?? '');
      const children = nested
        .filter((child) => (child.data?.['parent'] as { path: string } | undefined)?.path === path)
        .map((child) => toEntry(child));

      return {
        path,
        title: (route.data?.['title'] as string) ?? path,
        help: (route.data?.['help'] as string) ?? '',
        children,
      };
    };

    const hasFullAccess = this.authService.hasFullAccess();
    const isAdministrator = !!this.authService.currentUser()?.isAdministrator;

    return SECTION_ORDER.filter((name) => name !== 'Sistema' || hasFullAccess)
      .filter((name) => name !== 'Administração' || isAdministrator)
      .map((name) => {
        const entries = topLevel.filter((r) => r.data?.['section'] === name).map(toEntry);
        return { name, entries, entryPaths: entries.map((e) => e.path) };
      })
      .filter((section) => section.entries.length > 0);
  }
}
