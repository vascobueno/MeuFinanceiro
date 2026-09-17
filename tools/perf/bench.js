/**
 * Harness de medição de performance (Parte C do plano de desempenho).
 *
 * Mede p50/p95 de cada endpoint que a análise apontou como caro, além do tempo de parede do
 * Dashboard (as 5 chamadas em paralelo, que é o que o usuário sente).
 *
 * Roda TODOS os endpoints a cada execução, não só o que foi alterado: uma otimização pode
 * melhorar uma tela e piorar outra, e sem medir tudo isso passa batido.
 *
 * Uso:
 *   node bench.js [--api=https://localhost:5011] [--runs=15] [--label="baseline"]
 *
 * A contagem de round-trips ao banco NÃO sai daqui — ela vem do log do EF
 * (Microsoft.EntityFrameworkCore.Database.Command em Information). Ver README.md.
 */
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const arg = (name, fallback) => {
  const found = process.argv.find((a) => a.startsWith(`--${name}=`));
  return found ? found.split('=').slice(1).join('=') : fallback;
};

const API = arg('api', 'https://localhost:5011');
const RUNS = Number(arg('runs', 15));
const WARMUP = 3;
const LABEL = arg('label', new Date().toISOString().slice(0, 16));

const USER = { username: 'perfbench', password: 'Perf!Bench123' };

const isoPlus = (days) => {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
};

function parseCookies(res, jar = {}) {
  for (const raw of res.headers.getSetCookie?.() ?? []) {
    const [pair] = raw.split(';');
    const idx = pair.indexOf('=');
    jar[pair.slice(0, idx)] = pair.slice(idx + 1);
  }
  return jar;
}

const percentile = (sorted, p) => sorted[Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length))];

async function main() {
  const login = await fetch(`${API}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(USER),
  });
  if (!login.ok) {
    console.error('login falhou:', login.status, await login.text());
    if (login.status === 429) console.error('Rate limit de autenticação — aguarde e repita.');
    else console.error('Rode o bootstrap.js primeiro.');
    process.exit(1);
  }
  const jar = parseCookies(login);
  const cookie = Object.entries(jar)
    .map(([k, v]) => `${k}=${v}`)
    .join('; ');

  const get = (path) => fetch(`${API}/api/v1${path}`, { headers: { Cookie: cookie } });

  const endpoints = [
    ['GET /accounts', () => get('/accounts?page=1&pageSize=20')],
    ['GET /transactions', () => get('/transactions?page=1&pageSize=15')],
    ['GET /transactions (busca)', () => get('/transactions?page=1&pageSize=15&search=parcelado')],
    ['GET /projection +30d', () => get(`/projection?date=${isoPlus(30)}`)],
    ['GET /projection +60d', () => get(`/projection?date=${isoPlus(60)}`)],
    ['GET /audit', () => get('/audit?page=1&pageSize=20')],
    ['GET /installment-plans', () => get('/installment-plans?page=1&pageSize=10')],
    ['GET /recurring-transactions', () => get('/recurring-transactions')],
    ['GET /investments/summary', () => get('/investments/summary')],
    [
      // Reproduz exatamente o que o dashboard.component.ts pede, inclusive o maxTimelineEntries:
      // ele usa da projeção só os saldos, 5 eventos e dois agregados.
      'Dashboard (5 chamadas em paralelo)',
      () =>
        Promise.all([
          get('/accounts?page=1&pageSize=100'),
          get('/investments/summary'),
          get(`/projection?date=${isoPlus(30)}&maxTimelineEntries=5`),
          get(`/projection?date=${isoPlus(60)}&maxTimelineEntries=0`),
          get('/transactions?page=1&pageSize=100'),
        ]),
    ],
  ];

  console.log(`\n# Medição: ${LABEL}`);
  console.log(`API ${API} · ${RUNS} execuções (+${WARMUP} de aquecimento descartadas)\n`);
  console.log('| Endpoint | p50 (ms) | p95 (ms) | payload |');
  console.log('|---|---:|---:|---:|');

  for (const [nome, call] of endpoints) {
    const amostras = [];
    let bytes = 0;

    for (let i = 0; i < RUNS + WARMUP; i++) {
      const t0 = performance.now();
      const res = await call();
      const respostas = Array.isArray(res) ? res : [res];
      let total = 0;
      for (const r of respostas) {
        // Sem esta checagem, uma resposta 401/429 seria medida como se fosse sucesso — e o
        // rate limit do login já produziu uma rodada inteira de números falsos (~15 ms, 0 KB).
        if (!r.ok) {
          console.error(`\nFALHA em "${nome}": HTTP ${r.status} ${r.url}`);
          console.error('Medição abortada — números de resposta com erro não valem nada.');
          if (r.status === 429) console.error('Rate limit do login: aguarde alguns minutos e repita.');
          process.exit(1);
        }
        total += (await r.text()).length;
      }
      const dt = performance.now() - t0;

      if (i >= WARMUP) {
        amostras.push(dt);
        bytes = total;
      }
    }

    amostras.sort((a, b) => a - b);
    const kb = (bytes / 1024).toFixed(0);
    console.log(
      `| ${nome} | ${percentile(amostras, 50).toFixed(0)} | ${percentile(amostras, 95).toFixed(0)} | ${kb} KB |`,
    );
  }

  console.log('\nRound-trips ao banco: ver o log do EF (README.md).');
}

main();
