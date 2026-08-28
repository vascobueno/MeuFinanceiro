import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'mf-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class AppComponent {
  // Injetado aqui (raiz de tudo, antes do Login ou do Shell) pra já nascer com o skin salvo
  // — ThemeService só aplica data-theme quando alguém o injeta (providedIn: 'root' é lazy).
  private readonly themeService = inject(ThemeService);
}
