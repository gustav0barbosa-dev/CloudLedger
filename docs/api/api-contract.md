# Contrato da API — CloudLedger v1

Base URL: `/api/v1` · Formato: JSON · Autenticação: `Authorization: Bearer <access_token>`

Documentação interativa em `/swagger` (Development). Todos os erros seguem o formato:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Erro de validação",
  "status": 400,
  "detail": "Um ou mais campos são inválidos.",
  "correlationId": "0f5c...",
  "errors": { "amount": ["O valor deve ser maior que zero."] }
}
```

Listagens usam paginação por página:

```json
{ "items": [], "page": 1, "pageSize": 20, "totalItems": 0, "totalPages": 0 }
```

## Autenticação

| Método | Rota | Descrição |
| --- | --- | --- |
| POST | `/auth/register` | Cria usuário e retorna tokens. `201` |
| POST | `/auth/login` | Autentica e retorna tokens. `200` |
| POST | `/auth/refresh` | Rotaciona refresh token. `200` |
| POST | `/auth/logout` | Revoga refresh token. `204` |

`POST /auth/register`
```json
{ "name": "Ana Silva", "email": "ana@exemplo.com", "password": "Senha@12345" }
```
Resposta:
```json
{
  "accessToken": "eyJ...",
  "expiresIn": 3600,
  "refreshToken": "base64...",
  "user": { "id": "uuid", "name": "Ana Silva", "email": "ana@exemplo.com" }
}
```

Política de senha: mínimo 8 caracteres, ao menos 1 letra e 1 número.

## Perfil

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/users/me` | Perfil do usuário autenticado |
| PUT | `/users/me` | Atualiza nome e e-mail |
| PUT | `/users/me/password` | Troca de senha (senha atual + nova) |

## Contas

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/accounts?includeArchived=false` | Lista contas com saldo calculado |
| POST | `/accounts` | Cria conta |
| GET | `/accounts/{id}` | Detalhe |
| PUT | `/accounts/{id}` | Atualiza nome, tipo, saldo inicial |
| POST | `/accounts/{id}/archive` | Arquiva conta |
| POST | `/accounts/{id}/unarchive` | Desarquiva |
| DELETE | `/accounts/{id}` | Remove somente se sem movimentações |

`AccountResponse`:
```json
{
  "id": "uuid", "name": "Conta corrente", "type": "Checking",
  "currency": "BRL", "openingBalance": 1000.00, "balance": 1250.50,
  "isArchived": false, "createdAt": "2026-11-24T12:00:00Z"
}
```

## Categorias

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/categories?kind=Expense` | Categorias padrão + do usuário |
| POST | `/categories` | Categoria personalizada |
| PUT | `/categories/{id}` | Edita categoria do usuário (padrão: `403`) |
| DELETE | `/categories/{id}` | Remove categoria sem uso |

## Lançamentos

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/transactions` | Filtros: `from`, `to`, `type`, `categoryId`, `accountId`, `q`, `status`, `page`, `pageSize`, `sort` |
| POST | `/transactions` | Registra receita/despesa |
| GET | `/transactions/{id}` | Detalhe |
| PUT | `/transactions/{id}` | Corrige lançamento confirmado (gera auditoria) |
| POST | `/transactions/{id}/void` | Estorno lógico |

```json
{ "accountId": "uuid", "categoryId": "uuid", "type": "Expense",
  "amount": 50.00, "description": "Café", "occurredAt": "2026-11-22" }
```

## Transferências

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/transfers` | Lista paginada |
| POST | `/transfers` | Cria transferência simulada |
| POST | `/transfers/{id}/void` | Estorna transferência |

```json
{ "sourceAccountId": "uuid", "destinationAccountId": "uuid",
  "amount": 200.00, "occurredAt": "2026-11-23", "description": "Reserva" }
```

## Pagamentos recorrentes

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/recurring-payments?activeOnly=true` | Lista com próximas ocorrências |
| POST | `/recurring-payments` | Cria recorrência |
| PUT | `/recurring-payments/{id}` | Atualiza/ativa/desativa |
| GET | `/recurring-payments/{id}/occurrences?from=&to=` | Ocorrências projetadas |
| POST | `/recurring-payments/{id}/occurrences/{date}/pay` | Marca como paga (idempotente) |
| POST | `/recurring-payments/{id}/occurrences/{date}/skip` | Ignora ocorrência |

## Metas

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/goals?status=Active` | Lista com progresso |
| POST | `/goals` | Cria meta |
| PUT | `/goals/{id}` | Atualiza meta |
| POST | `/goals/{id}/movements` | Aporte (`Deposit`) ou retirada (`Withdrawal`) |
| GET | `/goals/{id}/movements` | Histórico de movimentos |
| POST | `/goals/{id}/cancel` | Cancela meta |

```json
{ "name": "Viagem de férias", "targetAmount": 2000.00,
  "deadline": "2027-07-01", "accountId": null }
```

## Dashboard

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/dashboard/summary?from=&to=` | Saldo total, receitas, despesas, economia, variações |
| GET | `/dashboard/series?from=&to=&granularity=day` | Série de receitas/despesas |
| GET | `/dashboard/expenses-by-category?from=&to=` | Distribuição de despesas |
| GET | `/dashboard/recent-transactions?take=5` | Últimas movimentações |
| GET | `/dashboard/upcoming-payments?days=30` | Próximos pagamentos |
| GET | `/dashboard/goals` | Progresso das metas |

## Auditoria e notificações

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/audit-events?from=&to=&page=` | Eventos do próprio usuário (MongoDB) |
| GET | `/notifications?unreadOnly=false` | Notificações internas |
| POST | `/notifications/{id}/read` | Marca como lida |
| POST | `/notifications/read-all` | Marca todas como lidas |

## Relatórios

| Método | Rota | Descrição |
| --- | --- | --- |
| POST | `/reports/transactions/export?from=&to=` | Gera CSV no S3 privado e retorna URL assinada (15 min) |

## Health

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/health/live` | Processo vivo |
| GET | `/health/ready` | Dependências prontas |

## Códigos de erro

| Código | Uso |
| --- | --- |
| 400 | Validação / regra de negócio violada |
| 401 | Não autenticado ou token inválido |
| 403 | Autenticado sem permissão (ex.: editar categoria padrão) |
| 404 | Recurso inexistente **ou de outro usuário** (RN-013) |
| 409 | Conflito (e-mail duplicado, ocorrência já paga, conta com movimentações) |
| 422 | Regra de domínio não atendida |
| 429 | Rate limit excedido |
| 500 | Erro inesperado (sem detalhes internos na resposta) |
