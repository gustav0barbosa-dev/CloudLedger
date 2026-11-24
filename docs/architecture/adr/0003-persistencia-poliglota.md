# ADR-0003 — Persistência poliglota com finalidades distintas

- **Status:** aceito
- **Data:** 2026-11-24

## Contexto

A documentação prevê PostgreSQL, MongoDB e DynamoDB. Sem casos de uso claros, o uso de três bancos pareceria artificial e aumentaria o custo operacional.

## Decisão

| Banco | Finalidade | Justificativa |
| --- | --- | --- |
| **PostgreSQL** | Fonte da verdade transacional (usuários, contas, lançamentos, recorrências, metas) | Relacionamentos, integridade referencial, agregações SQL e transações ACID |
| **MongoDB** | `audit_events` — trilha de auditoria | Estrutura de metadados variável por tipo de evento, escrita append-only, consultas por usuário/período |
| **DynamoDB** | `IdempotencyRecord` | Acesso por chave conhecida, TTL nativo, alta disponibilidade para consumidores de eventos |

Nenhum dado financeiro é duplicado entre os bancos. A auditoria guarda **metadados**, não o estado financeiro completo.

## Consequências

- **Positivas:** cada tecnologia resolve um problema concreto e demonstrável; baixo custo (Mongo local + DynamoDB on-demand).
- **Negativas:** três dependências de infraestrutura; consistência entre bancos não é transacional.
- **Mitigação:** auditoria é *best-effort* e não bloqueia a transação principal; idempotência tem o banco relacional como última linha de defesa (índice único).
