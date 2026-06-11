// Tiny client-side CSV export — no dependency, no backend endpoint.
// Builds a CSV string from already-loaded data and triggers a download.

type Cell = string | number;

/** RFC 4180 field escaping: wrap in quotes and double internal quotes when needed. */
function escapeField(value: Cell): string {
  const s = String(value);
  return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

/**
 * Build a CSV from headers + rows and download it as `filename`.
 * A UTF-8 BOM is prepended so Excel opens accented characters correctly.
 */
export function downloadCsv(filename: string, headers: string[], rows: Cell[][]): void {
  const bom = String.fromCharCode(0xfeff); // UTF-8 BOM for Excel
  const lines = [headers, ...rows].map((row) => row.map(escapeField).join(','));
  const csv = bom + lines.join('\r\n');

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
