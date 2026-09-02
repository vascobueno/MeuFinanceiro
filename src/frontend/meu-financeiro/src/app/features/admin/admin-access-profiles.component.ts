import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { GrupoService } from '../../core/services/grupo.service';
import { AdminMember } from '../../core/models/grupo.model';
import { GrupoAccessProfile, GrupoRole } from '../../core/models/auth.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Visão cruzada de todos os membros de todos os grupos — evita o Administrador ter que
 * entrar grupo por grupo só para ajustar o AccessProfile de alguém (admin-grupos
 * continua sendo a tela para ver/gerenciar grupos, esta é focada em perfis de acesso).
 * Lista completa em memória — busca e ordenação são feitas no cliente.
 */
@Component({
  selector: 'mf-admin-access-profiles',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatSortModule,
    ListStateComponent,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './admin-access-profiles.component.html',
  styleUrl: './admin-access-profiles.component.scss',
})
export class AdminAccessProfilesComponent implements OnInit {
  readonly GrupoRole = GrupoRole;
  readonly GrupoAccessProfile = GrupoAccessProfile;
  readonly displayedColumns = ['grupo', 'name', 'role', 'accessProfile'];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly members = signal<AdminMember[]>([]);
  readonly dataSource = new MatTableDataSource<AdminMember>([]);

  search = '';

  // Setter (não ngAfterViewInit) — a <table> só existe depois que loading() vira false, então
  // um @ViewChild lido em ngAfterViewInit pegaria undefined; o setter dispara de novo quando ela aparece.
  @ViewChild(MatSort) set sort(sort: MatSort) {
    if (sort) this.dataSource.sort = sort;
  }

  constructor(private readonly grupoService: GrupoService) {
    this.dataSource.filterPredicate = (member, filter) =>
      member.name.toLowerCase().includes(filter) ||
      member.email.toLowerCase().includes(filter) ||
      member.grupoName.toLowerCase().includes(filter);

    // "grupo" na coluna não bate com o campo grupoName — mapear explicitamente.
    this.dataSource.sortingDataAccessor = (item, property) =>
      property === 'grupo' ? item.grupoName : (item[property as keyof AdminMember] as string | number);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.grupoService.getAllMembers().subscribe({
      next: (members) => {
        this.members.set(members);
        this.dataSource.data = members;
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os membros.');
        this.loading.set(false);
      },
    });
  }

  applyFilter(): void {
    this.dataSource.filter = this.search.trim().toLowerCase();
  }

  updateAccessProfile(member: AdminMember, accessProfile: GrupoAccessProfile): void {
    this.actionError.set(null);
    this.grupoService.updateMemberAccessProfile(member.grupoId, member.userId, accessProfile).subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível alterar o perfil de acesso.'),
    });
  }
}
