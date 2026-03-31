export interface PermissionDto {
  id: string;
  name: string;
  description: string | null;
}

export interface RoleDto {
  id: string;
  name: string;
  description: string | null;
  createdAt: string;
}

export interface RoleWithPermissionsDto {
  id: string;
  name: string;
  description: string | null;
  permissions: PermissionDto[];
}

export interface RoleAssignDto {
  roleName: string;
}
