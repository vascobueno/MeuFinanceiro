import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { HouseholdService } from '../../core/services/household.service';
import { AdminMember } from '../../core/models/household.model';
import { HouseholdAccessProfile, HouseholdRole } from '../../core/models/auth.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Visão cruzada de todos os membros de todos os households — evita o Administrador ter que
 * entrar household por household só para ajustar o AccessProfile de alguém (admin-households
 * continua sendo a tela para ver/gerenciar households, esta é focada em perfis de acesso).
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
  readonly HouseholdRole = HouseholdRole;
  readonly HouseholdAccessProfile = HouseholdAccessProfile;
  readonly displayedColumns = ['household', 'name', 'role', 'accessProfile'];

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

  constructor(private readonly householdService: HouseholdService) {
    this.dataSource.filterPredicate = (member, filter) =>
      member.name.toLowerCase().includes(filter) ||
      member.email.toLowerCase().includes(filter) ||
      member.householdName.toLowerCase().includes(filter);

    // "household" na coluna não bate com o campo householdName — mapear explicitamente.
    this.dataSource.sortingDataAccessor = (item, property) =>
      property === 'household' ? item.householdName : (item[property as keyof AdminMember] as string | number);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.householdService.getAllMembers().subscribe({
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

  updateAccessProfile(member: AdminMember, accessProfile: HouseholdAccessProfile): void {
    this.actionError.set(null);
    this.householdService.updateMemberAccessProfile(member.householdId, member.userId, accessProfile).subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível alterar o perfil de acesso.'),
    });
  }
}
