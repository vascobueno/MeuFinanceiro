import { Component, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { HouseholdService } from '../../core/services/household.service';
import { AuthService } from '../../core/services/auth.service';
import { Household, HouseholdMember } from '../../core/models/household.model';
import { HouseholdAccessProfile, HouseholdRole } from '../../core/models/auth.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Tela de gestão de membros — reaproveitada em dois lugares: /membros (Owner, sobre o próprio
 * household, sem [householdId]) e a visão do Admin sobre qualquer household (com
 * [householdId]), evitando duplicar a UI de adicionar/remover membro.
 */
@Component({
  selector: 'mf-household-members',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ListStateComponent,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './household-members.component.html',
  styleUrl: './household-members.component.scss',
})
export class HouseholdMembersComponent implements OnInit {
  readonly HouseholdRole = HouseholdRole;
  readonly HouseholdAccessProfile = HouseholdAccessProfile;

  /** Ausente = household do próprio usuário (/me); presente = uso do Admin sobre esse household. */
  @Input() householdId?: string;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly household = signal<Household | null>(null);

  newEmail = '';
  newRole: HouseholdRole = HouseholdRole.Member;
  readonly saving = signal(false);

  constructor(
    private readonly householdService: HouseholdService,
    private readonly route: ActivatedRoute,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    // Rota /admin/households/:id/membros passa o id pela URL (sem withComponentInputBinding
    // no app.config.ts); rota /membros não tem :id nenhum, aí fica undefined = "o meu".
    this.householdId = this.route.snapshot.paramMap.get('id') ?? this.householdId;
    this.load();
  }

  /** Só quem pode gerenciar (Owner na própria tela, ou o Admin em qualquer household) vê as ações. */
  get canManage(): boolean {
    return this.householdId !== undefined || this.authService.currentUser()?.householdRole === HouseholdRole.Owner;
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    const request = this.householdId ? this.householdService.getHousehold(this.householdId) : this.householdService.getMine();
    request.subscribe({
      next: (household) => {
        this.household.set(household);
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

    const request = this.householdId
      ? this.householdService.addMemberToHousehold(this.householdId, { email: this.newEmail, role: this.newRole })
      : this.householdService.addMember({ email: this.newEmail, role: this.newRole });

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

  removeMember(userId: string): void {
    this.actionError.set(null);
    const request = this.householdId
      ? this.householdService.removeMemberFromHousehold(this.householdId, userId)
      : this.householdService.removeMember(userId);

    request.subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível remover este membro.'),
    });
  }

  updateAccessProfile(member: HouseholdMember, accessProfile: HouseholdAccessProfile): void {
    this.actionError.set(null);
    this.householdService.updateMemberAccessProfile(this.householdId, member.userId, accessProfile).subscribe({
      next: () => this.load(),
      error: (err) => this.actionError.set(err?.error?.detail ?? 'Não foi possível alterar o perfil de acesso.'),
    });
  }
}
