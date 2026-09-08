import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { GrupoService } from '../../core/services/grupo.service';
import { AdminMember } from '../../core/models/grupo.model';
import { GrupoAccessProfile, GrupoRole } from '../../core/models/auth.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Visão cruzada de todos os membros de todos os grupos — evita o Administrador ter que
 * entrar grupo por grupo só para ajustar o AccessProfile de alguém (admin-grupos
 * continua sendo a tela para ver/gerenciar grupos, esta é focada em perfis de acesso).
 * Lista completa em memória — busca e ordenação são feitas no cliente
 * (docs/migracao-primeng/00-plano.md, Fase 3). A busca multi-campo (nome/e-mail/grupo) virou
 * um computed local em vez do filterPredicate do MatTableDataSource — mais simples e sem
 * precisar aprender a API de filtro do p-table pra um caso que já é só um .filter() puro.
 */
@Component({
  selector: 'mf-admin-access-profiles',
  standalone: true,
  imports: [CommonModule, FormsModule, MfIconComponent, InputText, Select, Table, SortIcon, SortableColumn, ListStateComponent],
  templateUrl: './admin-access-profiles.component.html',
  styleUrl: './admin-access-profiles.component.scss',
})
export class AdminAccessProfilesComponent implements OnInit {
  readonly GrupoRole = GrupoRole;
  readonly GrupoAccessProfile = GrupoAccessProfile;
  readonly accessProfileOptions = [
    { label: 'Completo', value: GrupoAccessProfile.Completo },
    { label: 'Editor', value: GrupoAccessProfile.Editor },
    { label: 'Visualizador', value: GrupoAccessProfile.Visualizador },
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly members = signal<AdminMember[]>([]);

  search = '';
  private readonly searchTerm = signal('');

  readonly filteredMembers = computed(() => {
    const term = this.searchTerm();
    if (!term) return this.members();
    return this.members().filter(
      (m) => m.name.toLowerCase().includes(term) || m.email.toLowerCase().includes(term) || m.grupoName.toLowerCase().includes(term),
    );
  });

  constructor(private readonly grupoService: GrupoService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.grupoService.getAllMembers().subscribe({
      next: (members) => {
        this.members.set(members);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os membros.');
        this.loading.set(false);
      },
    });
  }

  applyFilter(): void {
    this.searchTerm.set(this.search.trim().toLowerCase());
  }

  updateAccessProfile(member: AdminMember, accessProfile: GrupoAccessProfile): void {
    this.actionError.set(null);
    this.grupoService.updateMemberAccessProfile(member.grupoId, member.userId, accessProfile).subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível alterar o perfil de acesso.'),
    });
  }
}
