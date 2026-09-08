import { Component, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { GrupoService } from '../../core/services/grupo.service';
import { AuthService } from '../../core/services/auth.service';
import { Grupo, GrupoMember } from '../../core/models/grupo.model';
import { GrupoAccessProfile, GrupoRole } from '../../core/models/auth.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Tela de gestão de membros — reaproveitada em dois lugares: /membros (Owner, sobre o próprio
 * grupo, sem [grupoId]) e a visão do Admin sobre qualquer grupo (com
 * [grupoId]), evitando duplicar a UI de adicionar/remover membro.
 */
@Component({
  selector: 'mf-grupo-members',
  standalone: true,
  imports: [CommonModule, FormsModule, MfIconComponent, ButtonDirective, InputText, Select, ListStateComponent],
  templateUrl: './grupo-members.component.html',
  styleUrl: './grupo-members.component.scss',
})
export class GrupoMembersComponent implements OnInit {
  readonly GrupoRole = GrupoRole;
  readonly GrupoAccessProfile = GrupoAccessProfile;
  readonly roleOptions = [
    { label: 'Member', value: GrupoRole.Member },
    { label: 'Owner', value: GrupoRole.Owner },
  ];
  readonly accessProfileOptions = [
    { label: 'Completo', value: GrupoAccessProfile.Completo },
    { label: 'Editor', value: GrupoAccessProfile.Editor },
    { label: 'Visualizador', value: GrupoAccessProfile.Visualizador },
  ];

  /** Ausente = grupo do próprio usuário (/me); presente = uso do Admin sobre esse grupo. */
  @Input() grupoId?: string;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly grupo = signal<Grupo | null>(null);

  newEmail = '';
  newRole: GrupoRole = GrupoRole.Member;
  readonly saving = signal(false);

  newMember = { name: '', username: '', email: '', role: GrupoRole.Member };
  readonly creatingMember = signal(false);

  constructor(
    private readonly grupoService: GrupoService,
    private readonly route: ActivatedRoute,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    // Rota /admin/grupos/:id/membros passa o id pela URL (sem withComponentInputBinding
    // no app.config.ts); rota /membros não tem :id nenhum, aí fica undefined = "o meu".
    this.grupoId = this.route.snapshot.paramMap.get('id') ?? this.grupoId;
    this.load();
  }

  /** Só quem pode gerenciar (Owner na própria tela, ou o Admin em qualquer grupo) vê as ações. */
  get canManage(): boolean {
    return this.grupoId !== undefined || this.authService.currentUser()?.grupoRole === GrupoRole.Owner;
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    const request = this.grupoId ? this.grupoService.getGrupo(this.grupoId) : this.grupoService.getMine();
    request.subscribe({
      next: (grupo) => {
        this.grupo.set(grupo);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set('Não foi possível carregar os membros.');
        this.loading.set(false);
      },
    });
  }

  addMember(): void {
    if (!this.newEmail.trim()) return;

    this.saving.set(true);
    this.actionError.set(null);

    const request = this.grupoId
      ? this.grupoService.addMemberToGrupo(this.grupoId, { email: this.newEmail, role: this.newRole })
      : this.grupoService.addMember({ email: this.newEmail, role: this.newRole });

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.newEmail = '';
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.actionError.set(err?.error?.detail ?? 'Não foi possível adicionar este membro.');
      },
    });
  }

  createMember(): void {
    if (!this.newMember.name.trim() || !this.newMember.username.trim() || !this.newMember.email.trim()) return;

    this.creatingMember.set(true);
    this.actionError.set(null);

    const request = this.grupoId
      ? this.grupoService.createMemberInGrupo(this.grupoId, this.newMember)
      : this.grupoService.createMember(this.newMember);

    request.subscribe({
      next: () => {
        this.creatingMember.set(false);
        this.newMember = { name: '', username: '', email: '', role: GrupoRole.Member };
        this.load();
      },
      error: (err) => {
        this.creatingMember.set(false);
        this.actionError.set(err?.error?.detail ?? 'Não foi possível criar este usuário.');
      },
    });
  }

  removeMember(userId: string): void {
    this.actionError.set(null);
    const request = this.grupoId
      ? this.grupoService.removeMemberFromGrupo(this.grupoId, userId)
      : this.grupoService.removeMember(userId);

    request.subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível remover este membro.'),
    });
  }

  updateAccessProfile(member: GrupoMember, accessProfile: GrupoAccessProfile): void {
    this.actionError.set(null);
    this.grupoService.updateMemberAccessProfile(this.grupoId, member.userId, accessProfile).subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível alterar o perfil de acesso.'),
    });
  }
}
