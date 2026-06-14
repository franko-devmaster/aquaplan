import { DestroyRef } from '@angular/core';

/**
 * F-015 — shared debounce helper, replacing three hand-rolled `setTimeout`
 * search debouncers that were never cleared on destroy (a late-firing timer
 * would mutate a singleton datastore filter off-screen and fire a phantom HTTP
 * request). The returned function debounces calls; the pending timer is cleared
 * automatically when the owning component is destroyed.
 *
 * Usage:
 *   private readonly debouncedSearch = debouncedSearch<string>(
 *     v => this.store.setSearch(v), this.destroyRef);
 *   onSearchChange(value: string) { this.debouncedSearch(value); }
 */
export function debouncedSearch<T>(
  apply: (value: T) => void,
  destroyRef: DestroyRef,
  delayMs = 300
): (value: T) => void {
  let timer: ReturnType<typeof setTimeout> | null = null;

  const clear = (): void => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
  };

  destroyRef.onDestroy(clear);

  return (value: T): void => {
    clear();
    timer = setTimeout(() => {
      timer = null;
      apply(value);
    }, delayMs);
  };
}
