export interface RegisterRequest {
  name: string;
  username: string;
  email: string;
  password: string;
  captchaToken?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
  captchaToken?: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export enum GrupoRole {
  Owner = 0,
  Member = 1,
}

/** Perfil de acesso de um Member dentro do próprio grupo — Owner é sempre Completo. */
export enum GrupoAccessProfile {
  Completo = 0,
  Editor = 1,
  Visualizador = 2,
}

export interface UserResponse {
  id: string;
  name: string;
  email: string;
  grupoId: string;
  grupoRole: GrupoRole;
  accessProfile: GrupoAccessProfile;
  isAdministrator: boolean;
  mustChangePassword: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ConfirmEmailRequest {
  email: string;
  token: string;
}

export interface ResendConfirmationRequest {
  username: string;
}

/** Registro não loga mais automaticamente — precisa confirmar o e-mail primeiro. */
export interface RegisterResponse {
  message: string;
}
