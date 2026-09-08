import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { GrupoService } from '../../core/services/grupo.service';
import { AdminGrupo } from '../../core/models/grupo.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Visão do Administrador: todos os grupos do sistema, não só o próprio.
 * Lista completa carregada de uma vez (sem paginação de servidor) — ordenação por coluna é
 * feita no cliente, nativamente pelo p-table (docs/migracao-primeng/00-plano.md, Fase 3) —
 * mais simples que o MatTableDataSource+MatSort de antes: como cada coluna ordena pelo campo
 * real do modelo (inclusive "Owner", que ordena por `ownerName`), não precisa de um
 * `sortingDataAccessor` customizado.
 */
@Component({
  selector: 'mf-admin-grupos',
  standalone: true,
  imports: [CommonModule, RouterLink, MfIconComponent, ButtonDirective, Table, SortIcon, SortableColumn, ListStateComponent],
  templateUrl: './admin-grupos.component.html',
  styleUrl: './admin-grupos.component.scss',
})
export class AdminGruposComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly grupos = signal<AdminGrupo[]>([]);

  constructor(private readonly grupoService: GrupoService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.grupoService.getAllGrupos().subscribe({
      next: (grupos) => {
        this.grupos.set(grupos);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os grupos.');
        this.loading.set(false);
      },
    });
  }
}
