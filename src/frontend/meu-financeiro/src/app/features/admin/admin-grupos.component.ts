import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { GrupoService } from '../../core/services/grupo.service';
import { AdminGrupo } from '../../core/models/grupo.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Visão do Administrador: todos os grupos do sistema, não só o próprio.
 * Lista completa carregada de uma vez (sem paginação de servidor) — ordenação por coluna é
 * feita no cliente via MatTableDataSource + MatSort.
 */
@Component({
  selector: 'mf-admin-grupos',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, MatButtonModule, MatTableModule, MatSortModule, ListStateComponent],
  templateUrl: './admin-grupos.component.html',
  styleUrl: './admin-grupos.component.scss',
})
export class AdminGruposComponent implements OnInit {
  readonly displayedColumns = ['name', 'owner', 'members', 'createdAt', 'actions'];
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly grupos = signal<AdminGrupo[]>([]);
  readonly dataSource = new MatTableDataSource<AdminGrupo>([]);

  // Setter (não `ngAfterViewInit`) porque a <table> só existe no DOM depois que loading()
  // vira false — um @ViewChild lido só em ngAfterViewInit pegaria undefined (a tabela ainda
  // não tinha sido renderizada pelo @if). O setter dispara de novo assim que ela aparece.
  @ViewChild(MatSort) set sort(sort: MatSort) {
    if (sort) this.dataSource.sort = sort;
  }

  constructor(private readonly grupoService: GrupoService) {
    // "owner" na tabela combina nome + e-mail — ordenar pelo nome, não pela coluna que não existe.
    this.dataSource.sortingDataAccessor = (item, property) =>
      property === 'owner' ? item.ownerName : (item[property as keyof AdminGrupo] as string | number);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.grupoService.getAllGrupos().subscribe({
      next: (grupos) => {
        this.grupos.set(grupos);
        this.dataSource.data = grupos;
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os grupos.');
        this.loading.set(false);
      },
    });
  }
}
