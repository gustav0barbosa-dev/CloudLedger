# Visão de arquitetura — CloudLedger

## 1. Estilo arquitetural

Monólito modular em camadas, implantado como uma unidade, com um worker separado para processamento assíncrono.

```
Angular SPA ──HTTPS/JSON──► CloudLedger.Api (ASP.NET Core)
                                   │
                     ┌─────────────┴──────────────┐
                     ▼                            ▼
            Application (casos de uso)     Worker (SQS consumer)
                     │                            │
                     ▼                            ▼
              Domain (regras)          Infrastructure (adapters)
                     │                            │
                     └──────────┬─────────────────┘
                                ▼
        PostgreSQL · MongoDB · DynamoDB · SQS · S3 · CloudWatch
```

O frontend **nunca** acessa bancos diretamente. Toda autorização é aplicada no backend.

## 2. Camadas

| Camada | Projeto | Responsabilidade |
| --- | --- | --- |
| Domain | `CloudLedger.Domain` | Entidades, enums, objetos de valor e invariantes. Sem dependência de EF/AWS/ASP.NET. |
| Application | `CloudLedger.Application` | Casos de uso, DTOs, validação, interfaces de repositório/serviços e orquestração. |
| Infrastructure | `CloudLedger.Infrastructure` | EF Core/PostgreSQL, MongoDB, DynamoDB, SQS, S3, JWT, hashing, implementações. |
| API | `CloudLedger.Api` | Controllers, autenticação, middleware de erros, Swagger, health checks, rate limiting. |
| Worker | `CloudLedger.Worker` | Consumo de SQS com idempotência, tratamento de mensagens inválidas (DLQ). |

Regra de dependência: `Domain ← Application ← Infrastructure ← Api/Worker`. O domínio não referencia nenhuma outra camada.

## 3. Fluxos

### 3.1 Requisição síncrona
1. Middleware de correlação (`X-Correlation-Id`) e tratamento global de exceções.
2. Autenticação JWT → `UserId` extraído de `sub` (nunca do corpo da requisição).
3. Controller valida DTO e delega ao caso de uso.
4. Caso de uso carrega agregados filtrando por `UserId`, aplica invariantes e persiste.
5. Evento de auditoria é publicado (Mongo) e, quando aplicável, evento de domínio na fila SQS.

### 3.2 Processamento assíncrono
1. Caso de uso grava `IdempotencyRecord` (DynamoDB) com `RequestHash` e status `Processing`.
2. Mensagem é publicada na fila SQS `cloudledger-domain-events`.
3. Worker consome, verifica idempotência, processa e grava `Completed`/`Failed`.
4. Falhas vão para DLQ após `maxReceiveCount`; mensagens inválidas são registradas sem bloqueio da fila.

### 3.3 Idempotência de pagamento de ocorrência
`POST /recurring-payments/{id}/occurrences/{date}/pay` usa chave determinística
`recurring:{id}:{date}`. O índice único `(RecurringPaymentId, DueDate)` é a última linha de defesa contra duplicidade no banco relacional.

## 4. Segurança

- Senhas: BCrypt (work factor 12). Nunca logadas.
- JWT HS256 com chave ≥ 32 bytes vinda de configuração/segredo; access token curto (60 min) e refresh token (7 dias) com rotação.
- Autorização por recurso: consultas sempre com `Where(x => x.UserId == currentUserId)`.
- Rate limiting nos endpoints de autenticação.
- CORS restrito às origens configuradas.
- HTTPS obrigatório fora de Development; HSTS habilitado.
- Segredos via variáveis de ambiente / AWS Secrets Manager; `.env` fora do versionamento.
- S3 privado com criptografia em repouso.

## 5. Observabilidade

- Logs estruturados (Serilog, JSON em produção) com `CorrelationId`, `UserId` e `TraceId`.
- Health checks: `/health/live` e `/health/ready` (PostgreSQL, Mongo).
- Métricas e alarmes no CloudWatch: erros 5xx, latência p95, profundidade da fila e DLQ.
- Auditoria funcional em MongoDB (`audit_events`) consultável pelo usuário.

## 6. Execução local

```bash
cp .env.example .env
docker compose up -d postgres mongo localstack   # dependências
dotnet run --project backend/CloudLedger.Api      # API + migrações automáticas em Dev
npm --prefix frontend/cloudledger-web start       # SPA
```

A API aplica migrações e faz seed de categorias padrão na inicialização em `Development`.
Usuário de demonstração (somente Development):

| E-mail | Senha |
| --- | --- |
| `demo@cloudledger.dev` | `Demo@12345` |

## 7. Testes

```bash
dotnet test                                   # unitários + integração
npm --prefix frontend/cloudledger-web test    # testes de componentes
npm --prefix tests/e2e test                   # Playwright (requer stack no ar)
```

- **Unitários:** cálculos de saldo, progresso de meta, validações, regras de transferência e recorrência.
- **Integração:** endpoints com banco relacional em memória (SQLite), isolamento entre usuários, formatos de erro e paginação.
- **E2E:** cadastro → conta → lançamento → transferência → meta → pagamento recorrente.

## 8. Decisões registradas

- [ADR-0001](adr/0001-monolito-modular.md) — monólito modular
- [ADR-0002](adr/0002-autenticacao-jwt.md) — autenticação própria com JWT
- [ADR-0003](adr/0003-persistencia-poliglota.md) — PostgreSQL, MongoDB e DynamoDB
- [ADR-0004](adr/0004-dinheiro-e-datas.md) — representação de dinheiro e datas
- [ADR-0005](adr/0005-saldo-derivado.md) — saldo derivado e estorno lógico
