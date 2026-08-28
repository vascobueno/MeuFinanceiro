import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { HouseholdService } from '../../core/services/household.service';
import { AdminHousehold } from '../../core/models/household.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Visão do Administrador: todos os households do sistema, não só o próprio.
 * Lista completa carregada de uma vez (sem paginação de servidor) — ordenação por coluna é
 * feita no cliente via MatTableDataSource + MatSort.
 */
@Component({
  selector: 'mf-admin-households',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, MatButtonModule, MatTableModule, MatSortModule, ListStateComponent],
  templateUrl: './admin-households.component.html',
  styleUrl: './admin-households.component.scss',
})
export class AdminHouseholdsComponent implements OnInit {
  readonly displayedColumns = ['name', 'owner', 'members', 'createdAt', 'actions'];
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly households = signal<AdminHousehold[]>([]);
  readonly dataSource = new MatTableDataSource<AdminHousehold>([]);

  // Setter (não `ngAfterViewInit`) porque a <table> só existe no DOM depois que loading()
  // vira false — um @ViewChild lido só em ngAfterViewInit pegaria undefined (a tabela ainda
  // não tinha sido renderizada pelo @if). O setter dispara de novo assim que ela aparece.
  @ViewChild(MatSort) set sort(sort: MatSort) {
    if (sort) this.dataSource.sort = sort;
  }

  constructor(private readonly householdService: HouseholdService) {
    // "owner" na tabela combina nome + e-mail — ordenar pelo nome, não pela coluna que não existe.
    this.dataSource.sortingDataAccessor = (item, property) =>
      property === 'owner' ? item.ownerName : (item[property as keyof AdminHousehold] as string | number);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.householdService.getAllHouseholds().subscribe({
      next: (households) => {
        this.households.set(households);
        this.dataSource.data = households;
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os households.');
        this.loading.set(false);
      },
    });
  }
}
