import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChangePasswordRequest,
  ConfirmEmailRequest,
  ForgotPasswordRequest,
  GrupoAccessProfile,
  GrupoRole,
  LoginRequest,
  RegisterRequest,
  RegisterResponse,
  ResendConfirmationRequest,
  ResetPasswordRequest,
  UserResponse,
} from '../models/auth.model';

// Só uma cópia leve pra UI não piscar "deslogado" durante o F5, antes do bootstrap do app
// terminar de chamar me() — NUNCA usada pra autorização, o servidor sempre revalida pelo
// cookie httpOnly (docs/12-seguranca.md).
const USER_CACHE_KEY = 'mf_user_cache';

/**
 * Autenticação via JWT emitido pela própria API, entregue em cookie httpOnly (nunca em
 * localStorage/JS) — docs/12-seguranca.md. login/register/refresh não devolvem mais token
 * nenhum no corpo, só o perfil do usuário; a sessão em si é o cookie.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUserSignal = signal<UserResponse | null>(this.readCachedUser());
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

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http
      .post<UserResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((user) => this.setCurrentUser(user)));
  }

  // Não loga automaticamente — o registro agora exige confirmar o e-mail primeiro
  // (docs/12-seguranca.md).
  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>(`${environment.apiUrl}/auth/register`, request);
  }

  confirmEmail(request: ConfirmEmailRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/confirm-email`, request);
  }

  resendConfirmation(request: ResendConfirmationRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/resend-confirmation`, request);
  }

  /** O cookie de refresh já vai sozinho — não precisa mandar nada no corpo. */
  refresh(): Observable<UserResponse> {
    return this.http
      .post<UserResponse>(`${environment.apiUrl}/auth/refresh`, {})
      .pipe(tap((user) => this.setCurrentUser(user)));
  }

  /** "Quem sou eu" — usado no bootstrap do app pra rehidratar a sessão a partir do cookie httpOnly após um F5. */
  me(): Observable<UserResponse> {
    return this.http.get<UserResponse>(`${environment.apiUrl}/auth/me`).pipe(tap((user) => this.setCurrentUser(user)));
  }

  /** Busca o cookie CSRF (XSRF-TOKEN) — precisa ser chamado uma vez antes de qualquer POST/PUT/DELETE fora do login/registro. */
  fetchCsrfToken(): Observable<void> {
    return this.http.get<void>(`${environment.apiUrl}/auth/csrf`);
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

  logout(): void {
    this.http.post(`${environment.apiUrl}/auth/logout`, {}).subscribe({ error: () => undefined });
    sessionStorage.removeItem(USER_CACHE_KEY);
    this.currentUserSignal.set(null);
  }

  private updateCurrentUser(patch: Partial<UserResponse>): void {
    const user = this.currentUserSignal();
    if (!user) return;
    this.setCurrentUser({ ...user, ...patch });
  }

  private setCurrentUser(user: UserResponse): void {
    sessionStorage.setItem(USER_CACHE_KEY, JSON.stringify(user));
    this.currentUserSignal.set(user);
  }

  private readCachedUser(): UserResponse | null {
    const raw = sessionStorage.getItem(USER_CACHE_KEY);
    return raw ? (JSON.parse(raw) as UserResponse) : null;
  }
}
