import { HttpParams } from '@angular/common/http';

/** Monta HttpParams a partir de um objeto simples, ignorando valores nulos/indefinidos/vazios. */
export function toHttpParams(source: Record<string, unknown>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(source)) {
    if (value === null || value === undefined || value === '') continue;
    params = params.set(key, String(value));
  }
  return params;
}
