# Versões do ambiente

## Ambiente confirmado

```text
Angular CLI 22.1.5
Angular 22.1.3
TypeScript 6.0.3
RxJS 7.8.2
Node 24.19.0
npm 11.17.0
```

## Regra

Não alterar essas versões como consequência automática da migração Material.

Material/CDK devem acompanhar a major do Angular.

Antes de qualquer update:

```bash
ng version
npm list @angular/core @angular/material @angular/cdk
```

Se houver incompatibilidade, parar e analisar antes de atualizar.

## Nota sobre Node

O ambiente atual está usando Node 24.19.0. Não alterar automaticamente. Caso o build ou uma dependência apresente incompatibilidade, registrar o erro e avaliar a matriz oficial de compatibilidade antes de mudar Node.
