import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GrupoAccessProfile } from '../models/auth.model';
import { AddMemberRequest, AdminGrupo, AdminMember, CreateMemberRequest, Grupo, GrupoMember } from '../models/grupo.model';

/**
 * Reaproveitada tanto pela tela de Membros (Owner, /me) quanto pela tela de Admin
 * (parametrizada por grupoId) — mesmo formato das duas rotas do backend.
 */
@Injectable({ providedIn: 'root' })
export class GrupoService {
  private readonly baseUrl = `${environment.apiUrl}/grupos`;
  private readonly adminUrl = `${environment.apiUrl}/admin`;

  constructor(private readonly http: HttpClient) {}

  getMine(): Observable<Grupo> {
    return this.http.get<Grupo>(`${this.baseUrl}/me`);
  }

  addMember(request: AddMemberRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/me/members`, request);
  }

  createMember(request: CreateMemberRequest): Observable<GrupoMember> {
    return this.http.post<GrupoMember>(`${this.baseUrl}/me/members/new`, request);
  }

  removeMember(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/me/members/${userId}`);
  }

  /** grupoId ausente = próprio grupo (Owner, /me); presente = uso do Admin. */
  updateMemberAccessProfile(grupoId: string | undefined, userId: string, accessProfile: GrupoAccessProfile): Observable<void> {
    const url = grupoId
      ? `${this.adminUrl}/grupos/${grupoId}/members/${userId}/access-profile`
      : `${this.baseUrl}/me/members/${userId}/access-profile`;
    return this.http.put<void>(url, { accessProfile });
  }

  // ----------------- admin -----------------

  getAllGrupos(): Observable<AdminGrupo[]> {
    return this.http.get<AdminGrupo[]>(`${this.adminUrl}/grupos`);
  }

  getAllMembers(): Observable<AdminMember[]> {
    return this.http.get<AdminMember[]>(`${this.adminUrl}/members`);
  }

  getGrupo(grupoId: string): Observable<Grupo> {
    return this.http.get<Grupo>(`${this.adminUrl}/grupos/${grupoId}/members`);
  }

  addMemberToGrupo(grupoId: string, request: AddMemberRequest): Observable<unknown> {
    return this.http.post(`${this.adminUrl}/grupos/${grupoId}/members`, request);
  }

  createMemberInGrupo(grupoId: string, request: CreateMemberRequest): Observable<GrupoMember> {
    return this.http.post<GrupoMember>(`${this.adminUrl}/grupos/${grupoId}/members/new`, request);
  }

  removeMemberFromGrupo(grupoId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/grupos/${grupoId}/members/${userId}`);
  }
}
