import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { HouseholdAccessProfile } from '../models/auth.model';
import { AddMemberRequest, AdminHousehold, AdminMember, Household } from '../models/household.model';

/**
 * Reaproveitada tanto pela tela de Membros (Owner, /me) quanto pela tela de Admin
 * (parametrizada por householdId) — mesmo formato das duas rotas do backend.
 */
@Injectable({ providedIn: 'root' })
export class HouseholdService {
  private readonly baseUrl = `${environment.apiUrl}/households`;
  private readonly adminUrl = `${environment.apiUrl}/admin`;

  constructor(private readonly http: HttpClient) {}

  getMine(): Observable<Household> {
    return this.http.get<Household>(`${this.baseUrl}/me`);
  }

  addMember(request: AddMemberRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/me/members`, request);
  }

  removeMember(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/me/members/${userId}`);
  }

  /** householdId ausente = próprio household (Owner, /me); presente = uso do Admin. */
  updateMemberAccessProfile(householdId: string | undefined, userId: string, accessProfile: HouseholdAccessProfile): Observable<void> {
    const url = householdId
      ? `${this.adminUrl}/households/${householdId}/members/${userId}/access-profile`
      : `${this.baseUrl}/me/members/${userId}/access-profile`;
    return this.http.put<void>(url, { accessProfile });
  }

  // ----------------- admin -----------------

  getAllHouseholds(): Observable<AdminHousehold[]> {
    return this.http.get<AdminHousehold[]>(`${this.adminUrl}/households`);
  }

  getAllMembers(): Observable<AdminMember[]> {
    return this.http.get<AdminMember[]>(`${this.adminUrl}/members`);
  }

  getHousehold(householdId: string): Observable<Household> {
    return this.http.get<Household>(`${this.adminUrl}/households/${householdId}/members`);
  }

  addMemberToHousehold(householdId: string, request: AddMemberRequest): Observable<unknown> {
    return this.http.post(`${this.adminUrl}/households/${householdId}/members`, request);
  }

  removeMemberFromHousehold(householdId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/households/${householdId}/members/${userId}`);
  }
}
