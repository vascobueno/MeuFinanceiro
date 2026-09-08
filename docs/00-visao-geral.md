# 00 — Visão Geral

## Objetivo
Substituir o controle operacional em Excel por um sistema web.

## Domínios
- Usuários e segurança
- Multi-usuário (Grupo — Owner/Member, perfis de acesso Completo/Editor/Visualizador)
- Administração do sistema (role Administrator, gestão de todos os grupos)
- Contas correntes
- Cartões
- Lançamentos
- Categorias
- Estabelecimentos/descrições
- Recorrências
- Parcelamentos
- Extratos bancários
- Faturas
- Conciliação
- Investimentos
- Projeção
- Auditoria
- Migração inicial

## Fontes externas
- Excel: migração inicial.
- Extrato bancário: conciliação da conta.
- Fatura: conciliação do cartão.
- Extrato consolidado: atualização de investimentos.

## Arquitetura
```text
Angular + PrimeNG + Tailwind CSS (ver docs/21-telas.md e docs/14-angular.md)
        |
      HTTPS
        |
ASP.NET Core Web API
        |
Entity Framework Core
        |
PostgreSQL

Arquivos -> MinIO/S3-compatible (docs/03-arquitetura.md)
Secrets -> variável de ambiente em produção (User Secrets em dev — docs/12-seguranca.md)
Logs -> Application Insights (opcional)
CI/CD -> GitHub Actions (build + testes; deploy é manual, sem Azure — docs/19-deploy.md)
```
