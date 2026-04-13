export interface UserListDto {
  id: string;
  userNumber: number;
  email: string;
  firstName: string;
  lastName: string;
  role: string | null;
  distributorId: string | null;
  distributorName: string | null;
  isActive: boolean;
  tenantId: string;
  createdAt: string;
}

export interface UserDetailDto {
  id: string;
  userNumber: number;
  email: string;
  firstName: string;
  lastName: string;
  role: string | null;
  distributorId: string | null;
  distributorName: string | null;
  isActive: boolean;
  tenantId: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface UserCreateDto {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  tenantId: string;
  role: string | null;
  distributorId: string | null;
}

export interface UserUpdateDto {
  email: string | null;
  firstName: string;
  lastName: string;
  role: string | null;
}
