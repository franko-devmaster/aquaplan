export type ChangeRequestType = 'Create' | 'Update' | 'Deactivate';
export type ChangeRequestStatus = 'Pending' | 'Approved' | 'Rejected';

export interface ChangeRequestDto {
  id: string;
  requestType: ChangeRequestType;
  status: ChangeRequestStatus;
  samplingLocationId: string | null;
  samplingLocationName: string | null;
  distributorId: string;
  distributorName: string | null;
  proposedName: string | null;
  proposedLocationCode: string | null;
  proposedLatitude: number | null;
  proposedLongitude: number | null;
  proposedDescription: string | null;
  requestedById: string;
  requestedByName: string | null;
  requestedAt: string;
  reviewedById: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  reviewComment: string | null;
}

export interface ChangeRequestCreateDto {
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  distributorId: string;
}

export interface ChangeRequestUpdateDto {
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
}

export interface ChangeRequestReviewDto {
  comment: string | null;
}
