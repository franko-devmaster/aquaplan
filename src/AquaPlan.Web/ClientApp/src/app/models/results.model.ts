// AQ-415 — Client-side counterparts to AquaPlan.Application.DTOs.Results.

export type ResultConformity = 'Pending' | 'Green' | 'Yellow' | 'Red';

export interface RecentResultDto {
  orderId: string;
  orderNumber: string;
  samplingLocationId: string | null;
  locationName: string;
  locationCode: string;
  programName: string;
  receivedAt: string;
  conformity: ResultConformity;
}

export interface ResultsMatrixLocationDto {
  id: string;
  code: string;
  name: string;
  sectorName: string;
  distributorId: string;
  distributorName: string;
}

export interface ResultsCellDto {
  locationId: string;
  date: string;
  orderId: string;
  orderNumber: string;
  programName: string;
  conformity: ResultConformity;
}

export interface ResultsMatrixDto {
  locations: ResultsMatrixLocationDto[];
  dates: string[];
  cells: ResultsCellDto[];
}

export interface ResultsMatrixFilter {
  distributorId?: string | null;
  sectorId?: string | null;
  anomaliesOnly?: boolean;
  dateFrom?: string | null;
  dateTo?: string | null;
}
