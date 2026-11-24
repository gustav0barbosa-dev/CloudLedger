# CloudLedger

Aplicação web demonstrativa de **gestão financeira pessoal**: contas, receitas, despesas, transferências simuladas, pagamentos recorrentes, metas de economia e um dashboard consolidado.

> **Aviso de escopo:** o CloudLedger **não é uma instituição financeira**, não custodia dinheiro e não realiza pagamentos ou transferências bancárias reais. Todas as movimentações são registros simulados. Não há integração bancária. Os dados usados em demonstração são fictícios.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Frontend | Angular + TypeScript (standalone components, signals) |
| Backend | C# / .NET 8 · ASP.NET Core Web API |
| Persistência transacional | PostgreSQL + Entity Framework Core |
| Auditoria | MongoDB |
| Idempotência / estado de eventos | Amazon DynamoDB |
| Mensageria | Amazon SQS |
| Armazenamento | Amazon S3 |
| Observabilidade | CloudWatch + logs estruturados |
| Testes | xUnit (unitário/integração) · Playwright (E2E) |
| Qualidade e CI/CD | GitHub Actions · Docker |

Arquitetura: **monólito modular** em camadas (Domain → Application → Infrastructure → API/Worker), conforme ADR-0001.

## Estrutura do repositório

```
CloudLedger/
├── frontend/cloudledger-web/     # SPA Angular
├── backend/
│   ├── CloudLedger.Domain/       # entidades, objetos de valor, invariantes
│   ├── CloudLedger.Application/  # casos de uso, DTOs, validação
│   ├── CloudLedger.Infrastructure/ # EF Core, Mongo, AWS, repositórios
│   ├── CloudLedger.Api/          # endpoints HTTP, autenticação, Swagger
│   └── CloudLedger.Worker/       # consumidores de fila / tarefas assíncronas
├── tests/
│   ├── CloudLedger.UnitTests/
│   ├── CloudLedger.IntegrationTests/
│   └── e2e/                      # Playwright
├── infrastructure/               # IaC (AWS CDK) e recursos de nuvem
├── docs/
│   ├── requirements/             # especificação do MVP e fonte original
│   ├── architecture/             # visão geral, modelo de dados, ADRs
│   ├── api/                      # contrato da API
│   └── design/                   # protótipo visual
├── scripts/                      # automação local
├── docker-compose.yml
└── .env.example
```

## Como executar localmente

Pré-requisitos: .NET SDK 8, Node.js 20+, Docker.

```bash
cp .env.example .env
docker compose up -d postgres mongo localstack
dotnet run --project backend/CloudLedger.Api
npm --prefix frontend/cloudledger-web install
npm --prefix frontend/cloudledger-web start
```

- API: <http://localhost:5080> · Swagger: <http://localhost:5080/swagger>
- Frontend: <http://localhost:4200>

Instruções detalhadas, incluindo migrações e testes, estão em [`docs/architecture/overview.md`](docs/architecture/overview.md).

## Documentação

- [Especificação do MVP](docs/requirements/mvp-spec.md)
- [Visão de arquitetura](docs/architecture/overview.md)
- [Modelo de dados](docs/architecture/data-model.md)
- [Contrato da API](docs/api/api-contract.md)
- [ADRs](docs/architecture/adr/)

## Roadmap (plano de implementação)

| Fase | Entrega | Situação |
| --- | --- | --- |
| 0 — Definição | Casos de uso, regras, modelo, wireframes, ADRs | ✅ |
| 1 — Fundação | Solução .NET, Angular, Docker, CI | ✅ |
| 2 — Núcleo financeiro | Auth, usuários, contas, categorias, lançamentos | ✅ |
| 3 — Dashboard | Resumo, séries, categorias, recentes, metas | ✅ |
| 4 — Recursos avançados | Transferências, recorrências, metas, auditoria Mongo | ✅ |
| 5 — AWS e eventos | SQS, Worker, DynamoDB, S3, CloudWatch | 🚧 |
| 6 — Finalização | E2E, segurança, acessibilidade, deploy | 🚧 |
