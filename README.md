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

### Execução sem Docker (modo InMemory)

Quando o Docker não estiver disponível, a API pode rodar com armazenamento em memória:

```bash
# PowerShell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:Database__Provider="InMemory"
$env:Mongo__Enabled="false"
dotnet run --project backend/CloudLedger.Api
```

O usuário demo é criado automaticamente: `demo@cloudledger.dev` / `Demo@12345`.

- API: <http://localhost:5080> · Swagger: <http://localhost:5080/swagger>
- Frontend: <http://localhost:4200>

### Worker e eventos de domínio

As operações financeiras gravam seus eventos em uma tabela de **outbox transacional** no mesmo
`SaveChanges` da transação. O `CloudLedger.Worker` publica esses eventos no SQS e os consome com
idempotência (estado em DynamoDB, chave `sqs-event:{EventId}`). Mensagens com falha voltam para a
fila e, após 5 tentativas, são direcionadas para a DLQ `cloudledger-domain-events-dlq`.
Quando `Aws__Enabled=false`, os adaptadores locais apenas registram logs — útil para desenvolvimento.

```bash
dotnet run --project backend/CloudLedger.Worker
```

### Testes

```bash
# Unitários e integração (integração exige PostgreSQL)
dotnet test tests/CloudLedger.UnitTests/CloudLedger.UnitTests.csproj
dotnet test tests/CloudLedger.IntegrationTests/CloudLedger.IntegrationTests.csproj

# Frontend (Karma)
npm --prefix frontend/cloudledger-web test -- --watch=false

# E2E (Playwright — inicia API e Angular automaticamente)
npm --prefix tests/e2e install
npm --prefix tests/e2e test
```

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
| 5 — AWS e eventos | Outbox transacional, SQS + Worker, idempotência em DynamoDB, adaptadores S3/SQS/DynamoDB | ✅ (CloudWatch alarms e deploy em AWS pendentes de validação) |
| 6 — Finalização | E2E (Playwright), autenticação, testes e CI | ✅ (paridade total com o protótipo e deploy pendentes) |

### Cobertura do protótipo

O frontend implementa as telas e ações do protótipo: CRUD de categorias, contas e lançamentos,
filtros/paginação, transferências, pagamentos recorrentes com pagamento/ignorar ocorrências,
metas com aportes/retiradas/cancelamento, notificações, auditoria, relatórios, perfil/senha e
configurações. Os cenários principais são validados por Playwright em `tests/e2e/`.

### Pendências conhecidas

- Validação ponta a ponta de S3, DynamoDB, DLQ e CloudWatch com LocalStack, seguida de deploy AWS.
- Comparação visual exata com imagens externas; a referência é `docs/design/CloudLedger_Prototipo.html`.
