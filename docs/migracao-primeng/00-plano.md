# Migração: Angular Material → PrimeNG + Tailwind CSS — Plano Mestre

## 0. Correção urgente de CI (pré-requisito, não relacionado à migração PrimeNG)

O job `frontend` do CI (`.github/workflows/ci.yml`) estava falhando no passo `npm ci` com
`ERESOLVE`:
```
npm error peer @angular/common@"22.1.4" from @angular/router@22.1.4
npm error Conflicting peer dependency: @angular/common@22.1.4
```
O `package.json`/`package-lock.json` de `main` estavam consistentes (todos os `@angular/*`
em `22.1.2`/`22.1.3`) — a falha acontecia em PRs (tipicamente do Dependabot, ver causa raiz
abaixo), não em `main` diretamente. Corrigido preventivamente aqui pra evitar que a próxima
PR automática (ou a própria migração PrimeNG, que também mexe em `package.json`) tropece no
mesmo problema.

**Causa raiz**: `.github/dependabot.yml` não agrupava os pacotes `@angular/*` — o Dependabot
abre uma PR bumpando um pacote do Angular por vez (ex.: só `@angular/router`). Os pacotes do
próprio framework Angular exigem versões **exatas e sincronizadas entre si** (peer deps do
tipo `"22.1.4"`, não `"^22.1.4"`), então bumpar um sozinho quebra a resolução dos outros.

**UI-(-1).1 — Alinhar versões `@angular/*`** — feito via `ng update`, alinhando todos os
pacotes do framework (`core`/`common`/`forms`/`router`/`animations`/`compiler`/
`platform-browser`/`platform-browser-dynamic`/`cdk`/`material`) na mesma versão.

**UI-(-1).2 — Agrupar pacotes Angular no Dependabot** — `.github/dependabot.yml` ganhou um
bloco `groups:` no ecossistema `npm`, reunindo `@angular/*` numa única PR combinada.

## 1. Contexto e objetivo

O frontend `src/frontend/meu-financeiro` (Angular 22.1, standalone, **zoneless** via
`provideZonelessChangeDetection()`, com `provideAnimationsAsync()`) usa Angular Material
22.1 em **40 dos 87** arquivos `.ts` de `src/app`. Objetivo: substituir Material por
**PrimeNG + Tailwind CSS**, mantendo o app funcional a cada passo (migração incremental,
tela por tela — não big-bang), sem quebrar o sistema de 7 skins de cor via `data-theme` nem
o comportamento zoneless.

## 2. Fora do escopo / regras obrigatórias

- Não alterar backend C#/ASP.NET Core, contratos de API, banco de dados ou regras
  financeiras.
- Não refatorar funcionalidades além do necessário pra migração visual.
- Não alterar rotas por motivo puramente visual.
- Angular Material permanece instalado durante toda a migração; PrimeNG e Material
  coexistem até a Fase 6.
- Cada ticket termina com build funcional (`npm run build` limpo) antes de seguir pro
  próximo.
- Remoção do Angular Material só ocorre na Fase 6, depois de todas as telas migradas e
  testadas.

## 3. Inventário verificado (ponto de partida documentado — substitui a necessidade de um
   ticket de inventário do zero)

### 3.1 Arquivos de estilo (`src/frontend/meu-financeiro/src/styles/`)
- `_tokens.scss` (70 linhas) — `--mf-*` custom properties (cores, superfícies, sombra,
  larguras de sidebar, radius) em `:root`, sobrescritas por `html[data-theme="X"]` pras 7
  skins. **Já independente do Material** — boa base pronta pra PrimeNG/Tailwind.
- `_material-theme.scss` (110 linhas) — um `@include mat.theme(...)` por skin, usando
  paletas M3 do Material como base e sobrescrevendo `--mat-sys-primary`/`--mat-sys-error`
  com `var(--mf-primary)`/`var(--mf-red)`. Será **apagado** na Fase 6 (não portável).
- `_layout.scss` (296 linhas) — overrides visuais do shell/sidebar em cima de
  `mat-sidenav`/`mat-toolbar`, com `!important` pra vencer especificidade do Material e um
  fix manual de `margin-left` porque `mat-sidenav-container` não recalcula sozinho em
  mudança de largura via CSS. Será **reescrito do zero** na Fase 2.
- `_finance.scss` (56 linhas) — classes utilitárias já livres de Material
  (`.mf-amount-positive/negative/neutral`, `.mf-card`, `.mf-badge`). Fica como está.

### 3.2 Componentes compartilhados (`shared/components/`)
10 componentes, 9 encapsulam Material:

| Componente | Testes hoje |
|---|---|
| `pagination.component.ts` | `.spec.ts` (lógica de paginação 0/1-based) |
| `date-range.component.ts` | — |
| `autocomplete.component.ts` | — |
| `money-input.component.ts` | `.spec.ts` (formatação + `markForCheck()`) |
| `status-badge.component.ts` | — |
| `stat-card.component.ts` | — |
| `list-state.component.ts` | — |
| `confirm-dialog.component.ts` | — |
| `help-dialog.component.ts` | — |
| `timeline.component.ts` | — (já sem Material, `CommonModule` puro — não precisa migração de lib, só QA visual quando o tema mudar) |

Único risco documentado no código: `date-range.component.ts` tem um workaround pra um bug
de loop infinito por igualdade referencial de `Date` no zoneless + `mat-datepicker`;
`money-input`/`autocomplete` usam `ChangeDetectorRef.markForCheck()` manual.

### 3.3 Shell (`layout/shell.component.ts`)
373 linhas totais (162 `.ts` + 211 `.html`). Módulos Material: `MatSidenavModule`,
`MatToolbarModule`, `MatListModule`, `MatIconModule`/`MatIconRegistry`, `MatButtonModule`,
`MatDialogModule`, `MatTooltipModule`. Usa `BreakpointObserver` (`@angular/cdk/layout`) —
único uso do CDK no projeto.

### 3.4 Ícones (`shared/icons/mf-nav-icons.ts`)
15 ícones SVG customizados, registrados via `iconRegistry.addSvgIconLiteral(...)` numa
função `registerMfNavIcons()` chamada em `shell.component.ts`.

### 3.5 Fontes/ícones no `index.html`
Hoje carrega: Google Fonts Roboto (300/400/500) + fonte ligature "Material Icons" + script
do Cloudflare Turnstile (não relacionado). Nenhuma referência a Tailwind/PrimeIcons/Inter
ainda.

### 3.6 18 pastas de features e uso de Material por tela

| Pasta | Telas | Módulos Material (arquivo mais denso) |
|---|---|---|
| `accounts` | account-list | 1 (CRUD simples) |
| `admin` | admin-grupos, admin-access-profiles | 1 cada |
| `auth` | login, register, forgot/reset/change-password, confirm-email | padrão repetido (card+form-field+input+button+icon+spinner) |
| `bank-reconciliation` | bank-reconciliation | 9 + core (`MAT_DATE_LOCALE`/`provideNativeDateAdapter`) |
| `card-reconciliation` | card-reconciliation | 9 + core (padrão idêntico ao anterior) |
| `cards` | card-list | 1 |
| `categories` | category-list | 1 |
| `dashboard` | dashboard | — |
| `grupo` | grupo-members | 1 |
| `help` | help-page | — |
| `installments` | installment-form | 8 + core |
| `invoices` | invoice-list | 1 |
| `investments` | investments | 9 + core (padrão idêntico à reconciliação) |
| `projection` | projection | 1 |
| `recurring` | recurring-list (4), recurring-form (8 + core) | — |
| `reports` | audit-log (3), income-projection-report (2, `@media print`), recurring-expenses-report (2, `@media print`) | — |
| `settings` | settings | 1 |
| `transactions` | transaction-list (8), transaction-form (9 + core) | mais denso do projeto |

### 3.7 CI atual (`.github/workflows/ci.yml`, job `frontend`)
`npm ci` → `npm audit --audit-level=high` → `npm test -- --watch=false --browsers=ChromeHeadless`
→ `npm run build`. Sem lint dedicado — esses 2 comandos (`npm test`, `npm run build`) são o
contrato de "não quebrei nada" a cada ticket.

### 3.8 Documentação que cita "Material" (atualizar na Fase 6)
`docs/00-visao-geral.md`, `docs/02-requisitos-nao-funcionais.md:3`, `docs/14-angular.md:33`,
`docs/33-versions.md:16,18`, `docs/21-telas.md` (título "Design System Material" +
princípios), `README.md` (seção Stack: "Angular + Angular Material").

## 4. Decisões técnicas

- **PrimeNG v22** (`primeng`, `primeicons`) + **licença Community gratuita** (gerar key em
  primeui.dev, configurar via `providePrimeNG` — projeto se enquadra no limite <5 devs/<10
  funcionários/<US$1M receita; dá suporte oficial a Angular 22 + zoneless completo).
- **Tailwind CSS v4** (`tailwindcss`, `@tailwindcss/postcss`) — config CSS-first via
  `@theme`, sem `tailwind.config.js` obrigatório.
- **`tailwindcss-primeui`** (plugin oficial) — expõe os design tokens do PrimeNG como
  variáveis/utilities do Tailwind, evitando dois sistemas de cor divergentes.
- Divisão de responsabilidade: **Tailwind** cuida de layout/spacing/utilitários; **PrimeNG**
  cuida de componentes complexos (table, dialog, datepicker, select, autocomplete). Não
  recriar componentes que o PrimeNG já oferece.
- Preservar a arquitetura das 7 skins trocando `mat.theme()` por `definePreset` do PrimeNG,
  lendo os mesmos tokens `--mf-*` já existentes em `_tokens.scss` como fonte única — não
  precisa recriar tokens, eles já são independentes do Material.
- Migração **incremental, tela por tela**; Material e PrimeNG coexistem até a Fase 6.
- `@angular/cdk` fica (único uso é `BreakpointObserver` no shell; não tem relação com
  licenciamento do Material nem é dependência do PrimeNG) — trocar por `matchMedia` nativo
  é opcional, avaliado só na Fase 2 se fizer sentido.

## 5. Estratégia de branches e commits

```
main
└── feature/migracao-primeng (branch guarda-chuva, opcional se preferir tudo em main)
    ├── feature/ui-primeng-00-fundacao
    ├── feature/ui-primeng-01-shared-kit
    ├── feature/ui-primeng-02-shell
    ├── feature/ui-primeng-03-telas-simples
    ├── feature/ui-primeng-04-auth
    ├── feature/ui-primeng-05-telas-complexas
    └── feature/ui-primeng-06-limpeza-final
```
Commits: `feat(ui): <o que mudou>` / `chore(ui): <o que mudou>` (ex.: `feat(ui): configure
primeng foundation`, `feat(ui): migrate transactions table`, `chore(ui): remove angular
material`).

## 6. Critérios globais de aceite (validar em todo ticket que tocar uma tela)

- **Build**: `npm run build` sem warning novo.
- **Execução local**: `npm start` sobe sem erro.
- **Regressão funcional**: dados carregam, APIs funcionam, criar/editar/excluir continuam
  funcionando, validações e navegação preservadas.
- **Responsividade**: desktop, tablet, mobile.
- **Qualidade visual**: hover, focus, disabled, loading, empty state, mensagens de erro e
  sucesso.
- **Skins**: alternar as 7 skins de cor e conferir que todas continuam corretas.

## 7. Definition of Done (por ticket)

```
✓ Código implementado
✓ npm run build executado e limpo
✓ npm test -- --browsers=ChromeHeadless --watch=false passando
✓ Teste funcional manual da(s) tela(s) afetada(s), nas 7 skins
✓ Nenhuma regressão conhecida
✓ Responsividade validada
✓ Commit realizado
```

## 8. Fases e tickets

### Fase 0 — Fundação

**UI-00.1 — Instalar PrimeNG + Tailwind**
- Arquivos: `package.json`, `package-lock.json`, novo `postcss.config.mjs` (raiz de
  `src/frontend/meu-financeiro`, ao lado de `angular.json`).
- Atividades:
  ```bash
  cd src/frontend/meu-financeiro
  npm install primeng@^22 primeicons
  npm install -D tailwindcss @tailwindcss/postcss postcss tailwindcss-primeui
  ```
  Criar `postcss.config.mjs`:
  ```javascript
  export default {
    plugins: {
      '@tailwindcss/postcss': {},
    },
  };
  ```
  Gerar a license key Community gratuita em [primeui.dev](https://primeui.dev) (conta
  grátis, projeto se enquadra no limite <5 devs/<10 funcionários/<US$1M receita) — guardar
  como `primeNgLicenseKey` em `environment.ts`/`environment.development.ts`, mesmo padrão já
  usado pra `turnstileSiteKey`.
- Aceite: `npm install`/`npm run build`/`npm start` funcionam; nenhuma tela existente quebra
  (Material continua 100% funcional, PrimeNG só instalado, ainda não usado em lugar nenhum).

**UI-00.2 — Configurar PrimeNG + Tailwind globalmente**
- Arquivos: `app.config.ts`, `styles.scss`.
- Atividades: adicionar `providePrimeNG` à lista de providers já existente (sem remover
  nenhum dos atuais — `provideZonelessChangeDetection()`, `provideRouter(routes)`,
  `provideHttpClient(...)`, `provideAnimationsAsync()`, `provideAppInitializer(...)`):
  ```typescript
  import { providePrimeNG } from 'primeng/config';
  import Aura from '@primeng/themes/aura';

  providePrimeNG({
    theme: {
      preset: Aura, // trocado pelo preset customizado na UI-00.4
    },
  }),
  ```
  Em `styles.scss`, no topo (antes dos `@use` de `styles/tokens` etc. já existentes):
  ```scss
  @import "tailwindcss";
  @plugin "tailwindcss-primeui";

  @theme {
    /* mapeia os tokens --mf-* existentes pro namespace do Tailwind, ex.: */
    --color-primary: var(--mf-primary);
    --color-surface: var(--mf-surface);
    /* completar com o restante dos tokens listados em _tokens.scss (seção 3.1) */
  }
  ```
  Criar uma rota/tela de validação temporária (ex.: `dev-primeng-check`, removida ao final
  da Fase 0) com `Button`, `InputText`, `Select`, `Dialog`, `Toast` do PrimeNG lado a lado
  com os equivalentes Material, especificamente pra verificar overlays (Dialog, Select
  dropdown) sob `provideZonelessChangeDetection()` — é a área de maior risco documentada no
  código atual (comentário em `app.config.ts` já alerta sobre isso pra `AutocompleteComponent`
  /`MoneyInputComponent`).
- Aceite: PrimeNG e Material renderizam juntos sem conflito de CSS; Dialog abre/fecha e
  Toast aparece/some corretamente; nenhum erro ou warning de change detection no console.

**UI-00.3 — Ícones e fontes**
- Arquivos: `index.html`, `shared/icons/mf-nav-icons.ts` (sem alteração de conteúdo nesta
  fase, só nota abaixo).
- Atividades: adicionar `primeicons` (via `angular.json` `styles` como asset local,
  preferível a CDN externo — seguir convenção do projeto pra outros assets). Manter a fonte
  Google Roboto (300/400/500) exatamente como está — não há "Inter" no projeto hoje.
  Manter os 15 ícones SVG customizados de `mf-nav-icons.ts` e seu registro via
  `MatIconRegistry` sem alteração — Material continua instalado até a Fase 6. A fonte
  ligature "Material Icons" só é removida do `index.html` na Fase 6 (UI-06.3).
- Aceite: ícones de navegação continuam aparecendo normalmente; `pi-*` (PrimeIcons)
  disponíveis pra uso nos componentes PrimeNG a partir daqui.

**UI-00.4 — Tema PrimeNG por skin**
- Arquivos: novo `styles/primeng-preset.ts`, `services/theme.service.ts` (ajuste, não
  reescrita).
- Atividades: criar o preset mapeando **nomes de token PrimeNG** pras **variáveis CSS**
  `--mf-*` (não pros valores resolvidos) — assim a troca de skin via `data-theme` no
  `<html>` continua funcionando sem precisar recriar o preset em runtime:
  ```typescript
  import { definePreset } from '@primeng/themes';
  import Aura from '@primeng/themes/aura';

  export const MfPreset = definePreset(Aura, {
    semantic: {
      primary: {
        500: 'var(--mf-primary)',
        // ...demais shades, todas apontando pra variáveis --mf-*
      },
    },
  });
  ```
  Trocar `preset: Aura` (da UI-00.2) por `preset: MfPreset`. Nenhuma mudança necessária no
  `ThemeService` em si — ele já só troca o atributo `data-theme` no `<html>`, e como o preset
  aponta pra `var(--mf-*)` (não pro valor fixo), a resolução acontece automaticamente pelo
  CSS cascade a cada troca de skin, igual já acontece hoje com o Material
  (`_material-theme.scss`).
- Aceite: cada uma das 7 skins aplica corretamente em componentes PrimeNG de teste (Button,
  Input, Select, Card, Table, Dialog, Toast) sem precisar de lógica JS adicional.

### Fase 1 — Shared UI kit (`shared/components/*`)

Migrar do mais simples ao mais arriscado, mantendo a mesma API pública (selector,
`@Input`/`@Output`) pra não precisar tocar em quem consome:

**UI-01.1** `pagination.component.ts` → PrimeNG `Paginator`; portar o locale PT-BR (hoje
provavelmente um `MatPaginatorIntl` customizado) pra API de locale do PrimeNG.

**UI-01.2** `stat-card`, `list-state`, `status-badge` → `Card`, `ProgressSpinner`/`Button`,
`Tag`/`Badge`. `status-badge` precisa de **reescrita total**: hoje deve depender de
`::ng-deep` pra colorir chip por status — recriar via classes/tokens do preset, sem
deep-pierce (PrimeNG não expõe internals equivalentes).

**UI-01.3** `confirm-dialog`, `help-dialog` → `Dialog`/`DynamicDialog`. Introduzir também
aqui um `NotificationService` fino (wrapping `MessageService`/`Toast` do PrimeNG) se algum
componente de feature disparar `MatSnackBar` diretamente fora desses wrappers — conferir
durante a execução deste ticket.

**UI-01.4** `money-input`, `autocomplete` → `InputText`/`AutoComplete`; revalidar
especificamente o `ChangeDetectorRef.markForCheck()` manual que já existe hoje — cada
atualização assíncrona de estado no zoneless precisa continuar disparando detecção.

**UI-01.5** (mais arriscado) `date-range` → `DatePicker` com `selectionMode="range"`;
revalidar se o `DatePicker` do PrimeNG tem o mesmo problema de igualdade referencial de
`Date` que forçou o workaround atual — não presumir que sumiu só por trocar de lib.

Aceite (por sub-ticket): mesma API pública preservada; testar cada skin nos componentes
afetados; `money-input.component.spec.ts` e `pagination.component.spec.ts` continuam
passando (ajustar apenas o necessário pra nova implementação interna, sem mudar o
comportamento testado).

### Fase 2 — Shell (`layout/shell.component.ts`)

**UI-02.1** Migrar sidenav/toolbar/list/tooltip/dialog/icon/button (373 linhas, 7 módulos
Material) para `Drawer` + `Menubar`/`Menu` do PrimeNG + Tailwind pro grid do layout.
Reescrever `_layout.scss` (296 linhas) do zero — hoje depende de classes internas do
Material (`.mdc-list-item__primary-text` etc.) que não existem no PrimeNG — usando os
tokens `--mf-*` e utilities do Tailwind. Preservar o comportamento responsivo do
`BreakpointObserver` (CDK fica, ver decisão na seção 4).

Aceite: navegação funciona (desktop e mobile), abrir/fechar do drawer funciona, estado ativo
do item de menu funciona, tooltips funcionam, dialog de ajuda/confirmação (já migrados na
Fase 1) continuam acionáveis a partir do shell.

### Fase 3 — Telas simples (CRUD com 1 módulo Material predominante)

**UI-03.1** `categories`, `accounts`, `cards`, `invoices`, `settings`, `help-page`,
`projection`, `admin` (grupos + perfis de acesso), `grupo-members`. Já se apoiam nos
wrappers da Fase 1 — trabalho é majoritariamente trocar `mat-card`/`mat-table`/
`mat-form-field`/`mat-select` por `Card`/`Table`/`InputText`+label Tailwind/`Select`.

Aceite: CRUD completo (criar/editar/excluir) em cada tela, dados corretos, responsivo.

### Fase 4 — Auth (6 telas, lote único)

**UI-04.1** `login`, `register`, `forgot-password`, `reset-password`, `change-password`,
`confirm-email` — estrutura repetida (card + form-field + input + button + icon + spinner;
`login` também tem checkbox). Migrar como um lote só, dado o padrão idêntico.

Aceite: login válido/inválido, loading, mensagens de erro, todas as validações client-side
preservadas, responsivo.

### Fase 5 — Telas complexas

**UI-05.1** `transaction-list` (8 módulos) / `transaction-form` (9 + core) — tela mais
densa do projeto. Preservar paginação/ordenação/filtros **server-side** (não virar
client-side ao trocar de `MatTableDataSource`+`MatSort` pra `p-table`).

**UI-05.2** `recurring-list` (4 módulos) / `recurring-form` (8 + core).

**UI-05.3** `bank-reconciliation` / `card-reconciliation` / `investments` — as três têm o
mesmo conjunto de 9 módulos Material + core (padrão idêntico); mesma estratégia de migração
nas três.

**UI-05.4** `installment-form` (8 + core).

**UI-05.5** `reports/*`: `audit-log` (3 módulos), `income-projection-report` (2 módulos,
`@media print` em `income-projection-report.component.scss:35`),
`recurring-expenses-report` (2 módulos, `@media print` em
`recurring-expenses-report.component.scss:35`). Testar especificamente a impressão/"Salvar
como PDF" depois da migração — o Material tinha CSS de impressão próprio que precisa de
equivalente com PrimeNG/Tailwind.

Aceite (cada sub-ticket): criar/editar/excluir, ordenação, paginação, filtros continuam
batendo os mesmos parâmetros na API; valores monetários e datas corretos; teste manual de
cada fluxo (essas telas não têm rede de testes automatizados de DOM).

### Fase 6 — Limpeza final

**UI-06.1** Auditoria final: `grep` por `@angular/material`, `mat-`, `Mat[A-Z]`, `--mat-`,
`--mdc-` em `src/app` e `src/styles` deve retornar zero.

**UI-06.2** Remover `@angular/material` do `package.json` e imports remanescentes; avaliar
separadamente se `@angular/cdk` sai também (só se `BreakpointObserver` for trocado por
`matchMedia` nativo — não é obrigatório).

**UI-06.3** Apagar `_material-theme.scss` e quaisquer overrides `.mat-mdc-*`/`.mdc-*`
remanescentes em `styles.scss`/`_layout.scss`; remover a fonte "Material Icons" ligature do
`index.html`.

**UI-06.4** Atualizar documentação: `docs/00-visao-geral.md`,
`docs/02-requisitos-nao-funcionais.md`, `docs/14-angular.md`, `docs/33-versions.md`,
`docs/21-telas.md` (renomear "Design System Material" → "Design System PrimeNG + Tailwind",
atualizar princípios), `README.md` (seção Stack: "Angular + Angular Material" →
"Angular + PrimeNG + Tailwind CSS").

**UI-06.5** QA manual completo em todas as 7 skins (light mode apenas — não há dark mode
hoje, não precisa testar).

Aceite: `npm install && npm run build && npm start` funcionam; suíte Karma inteira passa;
grep de resíduos = zero; todas as skins corretas.

## 9. Riscos e pontos de atenção (revalidar a cada fase, não só no fim)

- **Zoneless**: qualquer componente PrimeNG que atualize estado fora de um evento de
  template (timers, promises, callbacks de `ControlValueAccessor`) pode precisar do mesmo
  padrão de `ChangeDetectorRef.markForCheck()` já usado em `money-input`/`autocomplete`.
- **`date-range`**: não presumir que o `DatePicker` do PrimeNG resolve sozinho o bug de
  igualdade referencial de `Date` documentado no componente atual — testar explicitamente.
- **`status-badge`**: sem equivalente a `::ng-deep` no PrimeNG — reconstrução via tokens do
  preset ou classes utilitárias, nunca deep-pierce.
- **Sem rede de segurança de testes de DOM**: só 2 specs existentes (lógica pura, sem
  TestBed). QA manual por tela a cada fase é obrigatório; considerar smoke tests leves
  durante a Fase 1 (base mais reaproveitada, maior retorno).
- **Relatórios com impressão**: CSS de impressão (`@media print`) em 2 dos 3 relatórios
  precisa de reequivalência explícita, não só visual em tela.

## 10. Verificação

- **A cada fase**: `npm run build` limpo (sem warning de módulo Material órfão) +
  `npm test -- --browsers=ChromeHeadless --watch=false` (suíte Karma existente, hoje 2
  specs) + QA manual das telas migradas, alternando as 7 skins.
- **Ao final da Fase 6**: grep por `@angular/material`, `mat-`, `Mat[A-Z]`, `--mat-`,
  `--mdc-` em `src/app` e `src/styles` = zero resultados (fora de `node_modules`); CI verde
  (`npm audit` → `npm test` → `npm run build`, conforme `.github/workflows/ci.yml` atual).

## 11. Prompt padrão para executar cada ticket

```text
Estou executando o ticket UI-XX.Y do plano de migração PrimeNG do MeuFinanceiro
(docs/migracao-primeng/00-plano.md).

Objetivo:
[descrever o objetivo do ticket, copiado da seção correspondente]

Antes de alterar qualquer arquivo:
1. Leia os arquivos envolvidos listados no ticket.
2. Identifique todas as dependências (quem consome o componente/tela).
3. Não altere regras de negócio, contratos de API ou modelos financeiros.
4. Preserve a arquitetura Angular existente (standalone, zoneless).
5. Preserve a API pública de componentes compartilhados (selector, @Input/@Output).
6. Faça apenas as alterações necessárias para este ticket.

Depois da implementação:
1. Liste os arquivos alterados.
2. Explique cada alteração.
3. Rode npm run build e npm test -- --browsers=ChromeHeadless --watch=false.
4. Informe riscos ou pontos que precisam de teste manual (skins, responsividade).
```

## 12. Ordem recomendada de execução

```
UI-(-1).1  Alinhar versões @angular/* (corrige CI quebrado)
UI-(-1).2  Agrupar pacotes Angular no Dependabot

UI-00.1  Instalar PrimeNG + Tailwind
UI-00.2  Configurar globalmente
UI-00.3  Ícones e fontes
UI-00.4  Tema PrimeNG por skin

UI-01.1  Pagination
UI-01.2  Stat-card / List-state / Status-badge
UI-01.3  Confirm-dialog / Help-dialog / NotificationService
UI-01.4  Money-input / Autocomplete
UI-01.5  Date-range

UI-02.1  Shell

UI-03.1  Telas simples (9 telas)

UI-04.1  Auth (6 telas)

UI-05.1  Transactions
UI-05.2  Recurring
UI-05.3  Reconciliation + Investments
UI-05.4  Installment-form
UI-05.5  Reports (+ impressão)

UI-06.1  Auditoria final
UI-06.2  Remover @angular/material
UI-06.3  Limpar CSS/fontes
UI-06.4  Atualizar documentação
UI-06.5  QA final nas 7 skins
```
