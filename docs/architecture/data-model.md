# Modelo de dados — CloudLedger

## 1. Convenções

- Chaves primárias `Guid` (`uuid`) geradas na aplicação.
- Dinheiro: `numeric(18,2)`; nunca ponto flutuante.
- Datas financeiras: `date` (DateOnly). Instantes: `timestamptz` em UTC.
- Exclusão lógica: `IsArchived` em contas; `Status = Voided` em lançamentos/transferências.
- Toda tabela de domínio possui `UserId` indexado (isolamento multiusuário).

## 2. Diagrama lógico (PostgreSQL)

```
users 1───N accounts
users 1───N categories        (UserId NULL = categoria padrão global)
users 1───N transactions ──N──1 accounts
                        └──N──1 categories
users 1───N transfers ──1──► accounts (origem) / accounts (destino)
users 1───N recurring_payments ──1──N recurring_occurrences ──0..1──► transactions
users 1───N saving_goals ──1──N goal_movements
users 1───N notifications
users 1───N refresh_tokens
```

## 3. Tabelas

### users
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| Name | varchar(120) | not null |
| Email | varchar(256) | not null, unique (lowercase) |
| PasswordHash | text | not null |
| Status | int (enum) | Active/Disabled |
| CreatedAt | timestamptz | not null |

### accounts
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| UserId | uuid | FK users, index |
| Name | varchar(120) | not null |
| Type | int | Checking/Savings/Cash/Investment/Credit |
| Currency | char(3) | default BRL |
| OpeningBalance | numeric(18,2) | not null |
| IsArchived | boolean | default false |
| CreatedAt | timestamptz | not null |

Índice: `(UserId, IsArchived)`.

### categories
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| UserId | uuid NULL | FK users; NULL = padrão |
| Name | varchar(80) | not null |
| Kind | int | Income/Expense |
| IsDefault | boolean | not null |
| Color | varchar(7) | opcional |
| Icon | varchar(16) | opcional |

Índice único: `(UserId, Kind, lower(Name))` (categorias do usuário).

### transactions
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| UserId | uuid | FK, index |
| AccountId | uuid | FK accounts |
| CategoryId | uuid NULL | FK categories |
| Type | int | Income/Expense |
| Amount | numeric(18,2) | > 0 (check) |
| Description | varchar(200) | not null |
| OccurredAt | date | not null |
| Status | int | Confirmed/Voided |
| CreatedAt / UpdatedAt | timestamptz | |
| VoidedAt | timestamptz NULL | |
| ReversalOfId | uuid NULL | referência ao lançamento estornado |

Índices: `(UserId, OccurredAt desc)`, `(UserId, Type, OccurredAt)`, `(UserId, AccountId, OccurredAt)`, `(UserId, CategoryId, OccurredAt)`.

### transfers
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| UserId | uuid | FK, index |
| SourceAccountId | uuid | FK accounts |
| DestinationAccountId | uuid | FK accounts |
| Amount | numeric(18,2) | > 0 |
| OccurredAt | date | not null |
| Description | varchar(200) | |
| Status | int | Confirmed/Voided |
| CreatedAt | timestamptz | |

Check: `SourceAccountId <> DestinationAccountId`.

### recurring_payments
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| UserId | uuid | FK, index |
| AccountId | uuid | FK accounts |
| CategoryId | uuid NULL | FK categories |
| Description | varchar(200) | |
| Amount | numeric(18,2) | > 0 |
| Frequency | int | Weekly/Monthly/Yearly |
| NextDueDate | date | not null |
| IsActive | boolean | default true |
| CreatedAt | timestamptz | |

### recurring_occurrences
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| RecurringPaymentId | uuid | FK, index |
| DueDate | date | not null |
| Status | int | Pending/Paid/Skipped |
| TransactionId | uuid NULL | FK transactions |
| PaidAt | timestamptz NULL | |

**Unique:** `(RecurringPaymentId, DueDate)` — garante RN-008.

### saving_goals
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| UserId | uuid | FK, index |
| Name | varchar(120) | not null |
| TargetAmount | numeric(18,2) | > 0 |
| Deadline | date NULL | |
| AccountId | uuid NULL | FK accounts (conta vinculada) |
| Status | int | Active/Achieved/Cancelled |
| CreatedAt | timestamptz | |

### goal_movements
| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uuid | PK |
| SavingGoalId | uuid | FK, index |
| Amount | numeric(18,2) | > 0 |
| Type | int | Deposit/Withdrawal |
| OccurredAt | date | not null |
| CreatedAt | timestamptz | |

### notifications
| Id | UserId | Title | Message | Type | IsRead | CreatedAt | ResourceType | ResourceId |

### refresh_tokens
| Id | UserId | TokenHash (sha256) | ExpiresAt | RevokedAt | CreatedAt | ReplacedByTokenHash |

## 4. MongoDB — `audit_events`

```json
{
  "_id": "uuid",
  "userId": "uuid",
  "action": "TransactionCreated",
  "resourceType": "Transaction",
  "resourceId": "uuid",
  "occurredAt": "2026-11-24T12:00:00Z",
  "correlationId": "uuid",
  "metadata": { "amount": 50.0, "accountId": "uuid" }
}
```

Índices: `(userId, occurredAt desc)`, `(resourceType, resourceId)`.
Não são gravados valores sensíveis (senha, token) nem o histórico financeiro completo — apenas metadados necessários para rastreabilidade.

## 5. DynamoDB — `cloudledger-idempotency`

| Atributo | Papel |
| --- | --- |
| `PK` | partition key (`userId#scope#key`) |
| `IdempotencyKey` | chave de negócio (ex.: `recurring:{id}:{date}`) |
| `RequestHash` | SHA-256 do payload |
| `Status` | Processing/Completed/Failed |
| `ResultReference` | id do recurso gerado |
| `ExpiresAt` | TTL (epoch seconds) |

## 6. Regras de integridade

- `transactions.Amount > 0`, `transfers.Amount > 0`, `goal_movements.Amount > 0`.
- Categoria de despesa só em despesa e de receita só em receita.
- Conta arquivada não aceita novos lançamentos/transferências/recorrências.
- Conta com lançamentos/transferências não pode ser removida fisicamente.
- `SavingGoal.Status = Achieved` quando o acumulado ≥ valor-alvo; retiradas podem reabrir a meta.
