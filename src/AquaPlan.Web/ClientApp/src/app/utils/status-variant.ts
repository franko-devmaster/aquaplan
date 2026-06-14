import { StatusChipVariant } from '../components/status-chip/status-chip.component';

/**
 * F-013 — single source of truth for status → chip-variant mapping. Previously
 * this table was copy-pasted (with slight drift) across 7 components; any change
 * to status semantics required N synchronised edits. The functions are pure and
 * accept a raw string so both enum-typed and DTO-string callers can use them.
 */

const ORDER_STATUS_VARIANTS: Record<string, StatusChipVariant> = {
  New: 'draft',
  InProgress: 'info',
  Completed: 'success',
  Transmitted: 'success',
  Done: 'success',
  Cancelled: 'danger',
};

const ROUND_STATUS_VARIANTS: Record<string, StatusChipVariant> = {
  Draft: 'draft',
  Assigned: 'info',
  InProgress: 'info',
  Completed: 'success',
  Cancelled: 'danger',
};

const PLAN_STATUS_VARIANTS: Record<string, StatusChipVariant> = {
  Draft: 'draft',
  Submitted: 'info',
  Validated: 'success',
  Rejected: 'danger',
};

export function orderStatusVariant(status: string): StatusChipVariant {
  return ORDER_STATUS_VARIANTS[status] ?? 'draft';
}

export function roundStatusVariant(status: string): StatusChipVariant {
  return ROUND_STATUS_VARIANTS[status] ?? 'draft';
}

export function planStatusVariant(status: string): StatusChipVariant {
  return PLAN_STATUS_VARIANTS[status] ?? 'draft';
}
