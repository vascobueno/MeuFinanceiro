/**
 * Cria o usuário e as referências (contas, cartões, categorias) da massa de performance.
 *
 * Feito via API de propósito: usuário exige hash de senha e o grupo é criado pelo próprio
 * fluxo de registro. O volume pesado (20k lançamentos) fica no seed.sql, que é SQL cru.
 *
 * Uso: node bootstrap.js [--api https://localhost:5011]
 * Imprime no final o GrupoId e o UserId, que o seed.sql recebe como parâmetros.
 */
const API = (process.argv.find((a) => a.startsWith('--api=')) || '--api=https://localhost:5011').split('=')[1];

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const USER = {
  email: 'perf.bench@meufinanceiro.local',
  username: 'perfbench',
  password: 'Perf!Bench123',
  name: 'Perf Bench',
};

const ACCOUNTS = 10;
const CARDS = 5;

async function api(path, { method = 'GET', body, cookie, csrf } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (cookie) headers.Cookie = cookie;
  if (csrf) headers['X-XSRF-TOKEN'] = csrf;

  const res = await fetch(`${API}/api/v1${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  return res;
}

function parseCookies(res, jar = {}) {
  for (const raw of res.headers.getSetCookie?.() ?? []) {
    const [pair] = raw.split(';');
    const idx = pair.indexOf('=');
    jar[pair.slice(0, idx)] = pair.slice(idx + 1);
  }
  return jar;
}

const cookieHeader = (jar) =>
  Object.entries(jar)
    .map(([k, v]) => `${k}=${v}`)
    .join('; ');

(async () => {
  // Registro é idempotente do ponto de vista do bench: se já existe, seguimos para o login.
  const reg = await api('/auth/register', { method: 'POST', body: USER });
  if (reg.ok) {
    console.log('usuário criado — confirme o e-mail no banco antes de prosseguir:');
    console.log(`  UPDATE "AspNetUsers" SET "EmailConfirmed"=true WHERE "Email"='${USER.email}';`);
  } else {
    console.log('registro retornou', reg.status, '(provavelmente já existe) — seguindo');
  }

  const login = await api('/auth/login', {
    method: 'POST',
    body: { username: USER.username, password: USER.password },
  });
  if (!login.ok) {
    console.error('login falhou:', login.status, await login.text());
    console.error('Confirme o e-mail do usuário no banco e rode de novo.');
    process.exit(1);
  }
  const jar = parseCookies(login);

  const csrfRes = await api('/auth/csrf', { cookie: cookieHeader(jar) });
  parseCookies(csrfRes, jar);
  const csrf = jar['XSRF-TOKEN'];
  const cookie = cookieHeader(jar);

  const existing = await (await api('/accounts?page=1&pageSize=100', { cookie })).json();
  if (existing.totalItems >= ACCOUNTS) {
    console.log(`já existem ${existing.totalItems} contas — pulando criação de referências`);
  } else {
    for (let i = existing.totalItems; i < ACCOUNTS; i++) {
      await api('/accounts', {
        method: 'POST',
        cookie,
        csrf,
        body: { name: `Perf Conta ${i + 1}`, bank: `Banco ${i + 1}`, type: 1, initialBalance: 1000 * (i + 1) },
      });
    }
    for (let i = 0; i < CARDS; i++) {
      await api('/cards', {
        method: 'POST',
        cookie,
        csrf,
        body: { name: `Perf Cartao ${i + 1}`, brand: 'Visa', closingDay: 5, dueDay: 15, limit: 5000 },
      });
    }
    console.log(`criadas ${ACCOUNTS} contas e ${CARDS} cartões`);
  }

  const me = await (await api('/auth/me', { cookie })).json();
  console.log('\n--- parâmetros para o seed.sql ---');
  console.log('userId :', me.id);
  console.log('grupoId:', me.grupoId ?? '(veja a coluna GrupoId em GrupoMembers)');
})();
