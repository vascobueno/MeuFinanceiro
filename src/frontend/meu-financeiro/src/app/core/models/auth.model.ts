export interface RegisterRequest {
  name: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export enum HouseholdRole {
  Owner = 0,
  Member = 1,
}

/** Perfil de acesso de um Member dentro do próprio household — Owner é sempre Completo. */
export enum HouseholdAccessProfile {
  Completo = 0,
  Editor = 1,
  Visualizador = 2,
}

export interface UserResponse {
  id: string;
  name: string;
  email: string;
  householdId: string;
  householdRole: HouseholdRole;
  accessProfile: HouseholdAccessProfile;
  isAdministrator: boolean;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserResponse;
}
