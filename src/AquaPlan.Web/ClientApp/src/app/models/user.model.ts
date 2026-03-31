export interface DistributorSummary {
  id: string;
  name: string;
}

export interface UserListDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organization: string | null;
  isActive: boolean;
  tenantId: string;
  roles: string[];
  createdAt: string;
}

export interface UserDetailDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organization: string | null;
  isActive: boolean;
  tenantId: string;
  roles: string[];
  distributors: DistributorSummary[];
  createdAt: string;
  updatedAt: string | null;
}

export interface UserCreateDto {
  email: string;
  firstName: string;
  lastName: string;
  organization: string | null;
  password: string;
  tenantId: string;
  roles: string[];
  distributorIds: string[];
}

export interface UserUpdateDto {
  firstName: string;
  lastName: string;
  organization: string | null;
  roles: string[];
  distributorIds: string[];
}
