import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  GrupoAccessProfile,
  GrupoRole,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  UserResponse,
} from '../models/auth.model';

const ACCESS_TOKEN_KEY = 'mf_access_token';
const REFRESH_TOKEN_KEY = 'mf_refresh_token';
const USER_KEY = 'mf_user';

/**
 * Autenticação via JWT emitido pela própria API (docs/12-seguranca.md).
 * Nenhum segredo fica no front-end — apenas os tokens recebidos após login.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUserSignal = signal<UserResponse | null>(this.readStoredUser());
  readonly currentUser = this.currentUserSignal.asReadonly();

  /** Vê páginas de gestão do grupo (Configurações/Membros) — Owner sempre, ou Member com perfil Completo. */
  readonly hasFullAccess = computed(() => {
    const user = this.currentUser();
    return user?.grupoRole === GrupoRole.Owner || user?.accessProfile === GrupoAccessProfile.Completo;
  });

  /** Pode criar/editar/excluir dados financeiros — todos menos Visualizador. */
  readonly canWrite = computed(() => {
    const user = this.currentUser();
    return user?.grupoRole === GrupoRole.Owner || user?.accessProfile !== GrupoAccessProfile.Visualizador;
  });

  constructor(private readonly http: HttpClient) {}

  get accessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  get refreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  get isAuthenticated(): boolean {
    return !!this.accessToken;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken: this.refreshToken })
      .pipe(tap((response) => this.storeSession(response)));
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/reset-password`, request);
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/change-password`, request)
      .pipe(tap(() => this.updateCurrentUser({ mustChangePassword: false })));
  }

  private updateCurrentUser(patch: Partial<UserResponse>): void {
    const user = this.currentUserSignal();
    if (!user) return;

    const updated = { ...user, ...patch };
    localStorage.setItem(USER_KEY, JSON.stringify(updated));
    this.currentUserSignal.set(updated);
  }

  logout(): void {
    const refreshToken = this.refreshToken;
    if (refreshToken) {
      this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.currentUserSignal.set(null);
  }

  private storeSession(response: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    this.currentUserSignal.set(response.user);
  }

  private readStoredUser(): UserResponse | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as UserResponse) : null;
  }
}
