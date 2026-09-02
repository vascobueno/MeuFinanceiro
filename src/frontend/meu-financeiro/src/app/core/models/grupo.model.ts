import { GrupoAccessProfile, GrupoRole } from './auth.model';

export interface GrupoMember {
  userId: string;
  name: string;
  email: string;
  role: GrupoRole;
  accessProfile: GrupoAccessProfile;
}

export interface Grupo {
  id: string;
  name: string;
  members: GrupoMember[];
}

export interface AddMemberRequest {
  email: string;
  role: GrupoRole;
}

export interface CreateMemberRequest {
  name: string;
  username: string;
  email: string;
  role: GrupoRole;
}

export interface AdminGrupo {
  id: string;
  name: string;
  ownerName: string;
  ownerEmail: string;
  memberCount: number;
  createdAt: string;
}

/** Visão achatada de um membro com o grupo a que pertence — tela "Perfis de acesso". */
export interface AdminMember {
  grupoId: string;
  grupoName: string;
  userId: string;
  name: string;
  email: string;
  role: GrupoRole;
  accessProfile: GrupoAccessProfile;
}
