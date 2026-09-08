import { Component, Input, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MF_ICONS } from './mf-icons';

/**
 * Substitui `<mat-icon svgIcon="mf-x">` (Fase 6, UI-06.2 — remoção do Angular Material).
 * O Material injetava o SVG bruto no DOM e forçava `width:100%;height:100%` via CSS interna
 * pra fazer o ícone respeitar o tamanho do host (via classes como `.mf-page-title-icon`/
 * `.mf-nav-icon`, que setam `width`/`height`/`color` no próprio elemento), ignorando os
 * atributos `width`/`height` intrínsecos do SVG — `:host ::ng-deep svg` abaixo replica esse
 * comportamento exatamente. `bypassSecurityTrustHtml` é seguro aqui porque os SVGs vêm só de
 * `MF_ICONS` (literais estáticos deste código-fonte), nunca de entrada do usuário.
 */
@Component({
  selector: 'mf-icon',
  standalone: true,
  template: `<span [innerHTML]="svg"></span>`,
  styles: `
    :host {
      display: inline-flex;
      width: 24px;
      height: 24px;
      flex: none;
    }
    :host ::ng-deep svg {
      width: 100%;
      height: 100%;
      display: block;
    }
  `,
})
export class MfIconComponent {
  private readonly sanitizer = inject(DomSanitizer);

  @Input({ required: true }) name!: string;

  get svg(): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(MF_ICONS[this.name] ?? '');
  }
}
