# 25 — Alternativas de linguagem e de frontend

Pesquisa apenas — nenhuma mudança adotada. Compara a pilha atual (ASP.NET Core 10 + EF Core +
PostgreSQL no backend; Angular 22 sem zone.js, com signals, e PrimeNG no frontend — versões em
`docs/33-versions.md`) com C++, Rust, Java e outras linguagens, o Native AOT do .NET, outros
frameworks de frontend e outras bibliotecas de componentes.

Conclusão: manter a pilha atual. Nenhuma alternativa traz ganho que pague a reescrita.

Critérios, tirados do que o sistema usa de fato:

- valores decimais exatos (centavos);
- fatura em PDF com senha e com a posição de cada letra (`PdfPigTextExtractor`, docs/08);
- ORM com migrations;
- login completo, com confirmação de e-mail e bloqueio (docs/12);
- cerca de 400 testes de integração (docs/20);
- tabelas com ordenação e paginação no servidor, telas em português.

## Backend em C++

Possível, mas não recomendado: o custo é muito alto e quase nada melhora. O backend não é só
código C# — quase tudo vem de bibliotecas do .NET, e cada uma precisaria de equivalente:

| Hoje (.NET) | Em C++ | Observação |
| --- | --- | --- |
| ASP.NET Core (rotas, controllers, rate limiting, ProblemDetails, CORS, XSRF) | Drogon, Crow, Oat++ ou Boost.Beast | Frameworks menores; rate limiting, XSRF e ProblemDetails feitos à mão em boa parte |
| EF Core (LINQ, migrations, rastreio de mudanças) | sqlpp11, ODB, ou SQL direto com libpqxx | Nada do nível do EF; SQL escrito à mão e migrations à parte |
| ASP.NET Identity (cadastro, hash de senha, confirmação de e-mail, bloqueio) | Não existe pronto | Feito do zero; é a parte mais sensível de segurança |
| PdfPig (fatura em PDF com senha) | Poppler, PDFium ou MuPDF | Bons, mas atenção à licença: MuPDF é AGPL, Poppler é GPL |
| `decimal` (centavos exatos) | Não existe nativo | Biblioteca de decimal ou centavos em inteiros; um erro vira centavo errado no saldo |
| xUnit e WebApplicationFactory (~400 testes) | GoogleTest ou Catch2 | Testes de integração reescritos do zero |

- **Esforço:** na prática, um projeto novo. Conciliação, leitura de fatura, OFX e cartões
  seriam refeitos e validados de novo.
- **Segurança:** C++ não protege a memória. Um estouro de buffer num leitor de PDF ou OFX pode
  virar falha de segurança num sistema com dados financeiros.
- **Desempenho:** ganho pequeno. O tempo de cada requisição está no PostgreSQL e na rede, e o
  .NET 10 já está entre os mais rápidos para servidores web.
- **Manutenção:** compilar e empacotar (CMake, vcpkg ou Conan) é mais trabalhoso.
- **Frontend:** não muda, desde que rotas e JSON continuem iguais.

Faria sentido para uma parte específica e pesada (uma biblioteca chamada pelo C#), para
hardware muito limitado ou para um app embarcado. Nenhum desses casos se aplica hoje.

## Backend em Rust

Bem melhor que C++, mas ainda não compensa: o custo continua sendo reescrever tudo. O
compilador impede estouro de buffer, uso de memória já liberada e disputa entre threads (fora
de código `unsafe`), e o ecossistema web é maduro:

| Hoje (.NET) | Em Rust | Situação |
| --- | --- | --- |
| ASP.NET Core | axum (sobre tokio) com tower | Muito bom; rate limiting e CORS vêm do tower, ProblemDetails e XSRF feitos à mão |
| EF Core | sqlx (SQL verificado na compilação) ou SeaORM | Sem rastreio de mudanças; migrations em arquivos SQL |
| `decimal` | rust_decimal | Resolvido, com suporte no sqlx |
| ASP.NET Identity | argon2 para o hash, o resto à mão | Ponto fraco: cadastro, confirmação de e-mail, bloqueio e tokens do zero |
| PdfPig | lopdf, pdf-extract ou pdfium-render | Mais fraco; texto com a posição das letras (faturas do Pan) dá mais trabalho, ou traz uma biblioteca em C |
| xUnit e WebApplicationFactory | `cargo test`, com o axum chamado em memória | Bom, mas os ~400 testes reescritos |
| Compilação e pacotes | cargo | Muito melhor que CMake ou vcpkg |

Igual ao C++: o esforço de reescrever, o ganho pequeno de desempenho e o frontend intacto.
Próprio do Rust: a curva das regras de posse e empréstimo de memória, compilação mais lenta que
a do C# e, como ganho real, um executável único, pequeno e rápido de subir.

Para aprender Rust sem risco: reescrever uma parte isolada (leitura de OFX ou de fatura em
texto) e comparar com a atual usando os mesmos dados de teste.

## Native AOT do .NET

Custa caro e rende pouco aqui; ReadyToRun com arquivo único dá quase o mesmo ganho sem mudar
código.

Hoje o C# é compilado para IL e traduzido para a máquina na hora de rodar (JIT). Com o Native
AOT (*Ahead-of-Time*), a tradução acontece na publicação: executável único, sem .NET instalado,
que sobe em milissegundos, usa menos memória e só leva o código usado. Ativa-se com
`<PublishAot>true</PublishAot>` no `.csproj` e `dotnet publish -r win-x64` (ou `linux-x64`).

O custo: o programa não pode descobrir tipos na hora de rodar (reflexão) nem gerar código
dinamicamente.

| Parte | Funciona com Native AOT? | O que teria de mudar |
| --- | --- | --- |
| Controllers (`CardsController` e os demais) | Não; só Minimal APIs | Reescrever as rotas como `app.MapGet(...)` / `MapPost(...)`; os serviços ficam iguais |
| JSON (System.Text.Json) | Sim, com geração de código | Um `JsonSerializerContext` listando cada DTO |
| EF Core | Em parte; suporte experimental com consultas pré-compiladas | Consultas elaboradas podem não passar; migrations por script SQL à parte |
| ASP.NET Identity | Com restrições; usa reflexão | Configuração extra ou ficar de fora |
| Npgsql | Sim, nas versões recentes | Pouca coisa |
| PdfPig, rate limiting, ProblemDetails | A conferir | O `dotnet publish` com AOT lista os avisos de cada biblioteca |

Os testes rodam em JIT, então o executável AOT precisaria ser testado à parte; é um executável
por sistema operacional, e a publicação pede as ferramentas de C++. O AOT brilha em serverless,
linha de comando e containers que escalam rápido — não numa API que sobe uma vez e fica no ar.

Duas opções sem mudar código, combináveis:

1. **ReadyToRun** (`<PublishReadyToRun>true</PublishReadyToRun>`): pré-compila a maior parte e
   mantém o JIT para o resto; sobe bem mais rápido e funciona com tudo o que o projeto usa.
2. **Self-contained + arquivo único**
   (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`): executável com o
   .NET dentro, para máquina sem nada instalado; ~70 a 100 MB em vez de 20 a 30 MB.

## Backend em Java

A alternativa mais equilibrada: tem equivalente maduro para tudo, mas reescrever levaria ao
mesmo lugar, sem ganho.

| Hoje (.NET) | Em Java | Situação |
| --- | --- | --- |
| ASP.NET Core | Spring Boot (Spring MVC) | Equivalente direto; ProblemDetails (RFC 9457) pronto e controllers quase iguais |
| ASP.NET Identity e XSRF | Spring Security | Completo, mas o cadastro com confirmação de e-mail seria escrito; difícil de configurar |
| EF Core e migrations | Spring Data JPA (Hibernate) com Flyway ou Liquibase | Equivalente; o filtro por GrupoId igual ou como filtro do Hibernate |
| `decimal` | `BigDecimal` | Resolvido, porém verboso (`a.add(b)`) |
| PdfPig | Apache PDFBox | Melhor que o PdfPig: PDF com senha e posição de cada letra |
| xUnit e WebApplicationFactory | JUnit 5 com MockMvc ou Testcontainers | Equivalente; com Testcontainers, PostgreSQL de verdade nos testes |
| Native AOT | GraalVM Native Image | Mesma ideia e mesmas restrições; suporte oficial no Spring Boot |

- **Esforço:** alto, mas direto; controllers, serviços, entidades e migrations se traduzem
  quase classe por classe.
- **Segurança de memória e desempenho:** iguais ao C#; o Spring Boot usa mais memória e leva
  alguns segundos para subir.
- **Linguagem:** o Java 21/25 tem records e threads virtuais, mas é mais verboso e não tem LINQ
  nem `async/await`. O Kotlin fica mais próximo do C# e usa as mesmas bibliotecas.

| | C# (hoje) | Java | Rust | C++ |
| --- | --- | --- | --- | --- |
| Segurança de memória | sim | sim | sim | não |
| Bibliotecas para este sistema | completas | completas (PDF ainda melhor) | boas, com lacunas | fracas |
| Esforço de migrar | nenhum | alto, mas direto | alto | muito alto |
| O que se ganha | — | praticamente nada | executável pequeno | pouco |

Java faz sentido por motivo externo: equipe que já usa Java, servidor só com JVM ou aprender
Spring.

## Outras linguagens no mesmo nível

Há várias tão maduras quanto C# e Java; nenhuma paga a reescrita.

| Linguagem | Framework | Decimal | PDF com senha e posições | Login pronto | Tipagem |
| --- | --- | --- | --- | --- | --- |
| Kotlin | Spring Boot ou Ktor | `BigDecimal` | PDFBox (excelente) | Spring Security | estática, forte |
| TypeScript (Node.js) | NestJS | decimal.js ou o Decimal do Prisma | pdf.js da Mozilla (muito bom) | Passport, montado peça por peça | estática |
| Python | Django ou FastAPI | `Decimal`, nativo | pdfplumber ou pdfminer.six (muito bom) | Django: completo | opcional |
| Go | net/http, Gin ou Echo | shopspring/decimal | pdfcpu abre; extrair texto é fraco | feito à mão | estática, simples |
| PHP | Laravel | brick/math | smalot/pdfparser (médio) | Laravel: completo, com confirmação de e-mail | parcial |
| Ruby | Rails | `BigDecimal` | pdf-reader (médio) | Rails 8 tem gerador de login | dinâmica |

- **Kotlin:** a mais parecida com o C# na escrita, com as bibliotecas do Java.
- **TypeScript com NestJS:** a mesma linguagem do frontend, numa estrutura inspirada no
  Angular; os modelos de `core/models` poderiam ser compartilhados. O login teria de ser montado.
- **Python com Django:** o que vem mais pronto (usuários, login, migrations, painel de
  administração). Com tipagem opcional, erros que o C# pega ao compilar só aparecem nos testes.
- **Go:** executável único e pequeno, mas a leitura de PDF é a mais fraca da tabela.
- **PHP (Laravel) e Ruby (Rails):** maduros e produtivos, para trás em tipagem e em PDF.

Ordem, se houver motivo para trocar: TypeScript com NestJS (uma linguagem só no projeto),
Kotlin (JVM ou sintaxe parecida com o C#), Python com Django (menos código, mais coisa pronta).

## Frontend: Angular, React, Vue, Svelte e Blazor

Manter o Angular: está moderno e nada nas telas pede outro framework. Trocar o frontend não
mexe em regra nenhuma (elas estão no backend), mas reescreve todas as telas. O PrimeNG tem
irmãos com os mesmos componentes e visual — PrimeReact e PrimeVue —, então trocar de framework
não obriga a trocar a biblioteca de componentes.

| | Angular (hoje) | React | Vue | Svelte |
| --- | --- | --- | --- | --- |
| O que é | framework completo | biblioteca de interface | framework progressivo | compilador de componentes |
| Já vem pronto | rotas, formulários, HTTP, injeção de dependência, testes | só a interface (React Router, TanStack Query… à escolha) | rotas e estado oficiais (Vue Router, Pinia) | SvelteKit traz rotas e servidor |
| Como se escreve | TypeScript com template HTML à parte | JSX: HTML dentro do JavaScript | template HTML, parecido com o do Angular | HTML com pouca cerimônia |
| Reatividade | signals | estado e re-render (`useState`) | refs reativas, parecidas com signals | runes, parecidas com signals |
| Componentes como o PrimeNG | PrimeNG | PrimeReact, MUI, shadcn/ui | PrimeVue, Vuetify | menos opções |
| Aprendizado | o mais longo | médio | o mais curto | curto |
| Ecossistema | grande, forte em empresas | o maior | grande | menor |

- **React:** maior ecossistema e mais vagas, mas cada projeto monta a própria pilha.
- **Vue:** o mais próximo do Angular (`v-if` no lugar de `@if`); com o PrimeVue, a migração
  mais direta.
- **Svelte:** menos código e páginas mais leves, poucos componentes para telas administrativas.
- **Angular:** para muitos formulários e tabelas, ter tudo padronizado ajuda; signals, sem
  zone.js e `@if`/`@for` resolveram as críticas antigas.

**Blazor** é o frontend do próprio .NET, com telas em C#: os DTOs do backend seriam usados
direto na tela, sem a cópia em TypeScript. Contra: o modo WebAssembly baixa o runtime e a
primeira abertura fica lenta, o modo Server mantém uma conexão aberta o tempo todo, e há menos
componentes (MudBlazor, Radzen).

## Bibliotecas de componentes (UI)

Manter o PrimeNG: a tabela dele é a peça mais usada do sistema e o visual próprio (cores
`--mf-*`, 7 skins — `src/styles/_tokens.scss`) já está construído por cima dele.

| | PrimeNG (hoje) | Angular Material | NG-ZORRO | Taiga UI | spartan/ui |
| --- | --- | --- | --- | --- | --- |
| Origem | PrimeTek | Google | Ant Design (Alibaba) | T-Bank (banco russo) | comunidade (o "shadcn" do Angular) |
| Componentes | 90+ | ~35, mais o CDK | 70+ | 100+ | ~40, crescendo |
| Tabela | a mais completa: ordenação, filtro, paginação, seleção, edição na linha, colunas congeladas | básica (`mat-table` com sort e paginator) | muito completa | boa | TanStack Table, montada à mão |
| Visual | neutro, personalizável | Material Design | Ant Design (corporativo) | limpo, moderno | o que você desenhar |
| Tema próprio | tokens de design, ou modo sem estilo com Tailwind | tokens do Material 3, preso à linguagem Material | variáveis Less/CSS | variáveis CSS | Tailwind, liberdade total |
| Português | tradução por configuração (`shared/i18n/primeng-translation-pt-br.ts`) | por `LOCALE_ID`, poucas frases próprias | pt_BR incluso | parcial | você escreve os textos |
| Para finanças | campo de moeda (`p-inputNumber`) | nada específico | campo numérico | o mais forte: campo de cartão, dinheiro, máscaras | nada específico |
| Acessibilidade | boa | a melhor | boa | boa | boa (usa o CDK) |
| Estabilidade entre versões | ponto fraco: a v18 reescreveu o tema | muito estável | estável | estável | jovem, muda bastante |
| Licença | MIT (temas premium pagos) | MIT | MIT | Apache 2.0 | MIT |

Equivalentes em React e Vue:

| Angular | React | Vue |
| --- | --- | --- |
| PrimeNG | PrimeReact | PrimeVue |
| Angular Material | MUI (Material UI) | Vuetify |
| NG-ZORRO | Ant Design | Ant Design Vue |
| spartan/ui | shadcn/ui (a origem da ideia) | shadcn-vue |
| — | Mantine, Chakra UI | Quasar, Naive UI |

Se o problema for depender do visual do PrimeNG, o passo intermediário é o modo sem estilo
(*unstyled*) dele com o Tailwind que o projeto já carrega. Fora isso, só a Taiga UI teria
vantagem real e específica para finanças.

## Recomendações

| Área | Recomendação | Se houver motivo para mudar |
| --- | --- | --- |
| Linguagem do backend | Manter C# (.NET 10) | TypeScript com NestJS, Kotlin ou Python com Django, nessa ordem |
| Publicação | ReadyToRun com self-contained e arquivo único, se for instalar em outra máquina | Native AOT só com Minimal APIs e EF Core validado |
| Testes | Testcontainers para .NET, rodando contra PostgreSQL de verdade | — |
| Framework do frontend | Manter Angular | Vue com PrimeVue (migração mais suave), React (mercado) ou Blazor (só C#) |
| Biblioteca de UI | Manter PrimeNG | PrimeNG sem estilo com Tailwind, ou Taiga UI pelos componentes financeiros |
| Aprender outra linguagem | Reescrever uma parte isolada (leitura de OFX ou de fatura em texto) e comparar com os mesmos dados de teste | — |

A sugestão de Testcontainers vem de uma limitação conhecida da suíte atual (docs/20): o EF Core
InMemory ignora índices únicos, chaves estrangeiras e transações, e testes contra PostgreSQL de
verdade pegariam esses casos.
