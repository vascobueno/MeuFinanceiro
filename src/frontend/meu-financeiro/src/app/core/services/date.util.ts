/**
 * Datas ISO (`yyyy-MM-dd`) vindas da API, lidas no fuso LOCAL.
 *
 * `new Date('2026-10-01')` é interpretado pelo JavaScript como meia-noite **UTC**. Em qualquer
 * fuso negativo — São Paulo é UTC−3 — isso vira 30/09 local, e `getMonth()` passa a devolver o
 * mês anterior. Foi exatamente esse deslocamento que travou a navegação entre ciclos de fatura
 * em cartões que fecham no dia 1: avançar não saía do lugar e voltar pulava dois meses.
 *
 * O backend manda `DateOnly`, que é dia de calendário sem hora nem fuso. Interpretá-lo como um
 * instante UTC é a tradução errada: aqui ele é montado como data local, sem conversão nenhuma.
 */

/** Converte `yyyy-MM-dd` (ou um ISO completo) numa Date local, preservando o dia. */
export function parseIsoDate(iso: string): Date {
  const [year, month, day] = iso.substring(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** Formata uma Date local como `yyyy-MM-dd`, sem passar por UTC. */
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** O primeiro dia do mês de uma data ISO, como `yyyy-MM-01`. */
export function startOfMonthIso(iso: string): string {
  const date = parseIsoDate(iso);
  return toIsoDate(new Date(date.getFullYear(), date.getMonth(), 1));
}

/**
 * Anda `months` meses (negativo volta) sobre o mês de uma data ISO, devolvendo `yyyy-MM-01`.
 * Sempre a partir do dia 1, que nunca é encurtado — não há o problema de fim de mês aqui.
 */
export function shiftMonthIso(iso: string, months: number): string {
  const date = parseIsoDate(iso);
  return toIsoDate(new Date(date.getFullYear(), date.getMonth() + months, 1));
}
