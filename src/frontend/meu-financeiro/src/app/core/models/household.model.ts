import { HouseholdAccessProfile, HouseholdRole } from './auth.model';

export interface HouseholdMember {
  userId: string;
  name: string;
  email: string;
  role: HouseholdRole;
  accessProfile: HouseholdAccessProfile;
}

export interface Household {
  id: string;
  name: string;
  members: HouseholdMember[];
}

export interface AddMemberRequest {
  email: string;
  role: HouseholdRole;
}

export interface AdminHousehold {
  id: string;
  name: string;
  ownerName: string;
  ownerEmail: string;
  memberCount: number;
  createdAt: string;
}

/** Visão achatada de um membro com o household a que pertence — tela "Perfis de acesso". */
export interface AdminMember {
  householdId: string;
  householdName: string;
  userId: string;
  name: string;
  email: string;
  role: HouseholdRole;
  accessProfile: HouseholdAccessProfile;
}
