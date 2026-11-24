# ADR-0001 — Monólito modular em camadas

- **Status:** aceito
- **Data:** 2026-11-24

## Contexto

O CloudLedger é um projeto demonstrativo/portfólio. A documentação de origem sugere começar com um backend modular em ASP.NET Core, e não com vários microsserviços. A complexidade operacional de microsserviços (descoberta, rede, transações distribuídas, observabilidade fragmentada) não se justifica no primeiro dia.

## Decisão

Adotar um **monólito modular** com quatro projetos de produção:

- `CloudLedger.Domain` — entidades e regras, sem dependências externas.
- `CloudLedger.Application` — casos de uso, DTOs, validação, interfaces.
- `CloudLedger.Infrastructure` — EF Core/PostgreSQL, MongoDB, DynamoDB, SQS, S3.
- `CloudLedger.Api` e `CloudLedger.Worker` — bordas de entrada.

A regra de dependência é unidirecional. Módulos de domínio (Accounts, Transactions, Recurring, Goals, Audit) são separados por pastas e namespaces, com contratos explícitos.

## Consequências

- **Positivas:** implantação simples, transações ACID locais, um único pipeline de CI, refatoração barata.
- **Negativas:** um único processo de API concentra o deploy; escala é vertical/horizontal do monólito.
- **Mitigação:** worker separado para cargas assíncronas e limites de módulo que permitem extração futura de serviços.
