/**
 * F-027 — shared blob download helper, replacing the duplicated
 * `createElement('a') / click() / revokeObjectURL()` dance in the CSV/PDF
 * export methods. Creates a temporary object URL, triggers the download and
 * always revokes the URL afterwards.
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = window.URL.createObjectURL(blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
  } finally {
    window.URL.revokeObjectURL(url);
  }
}
