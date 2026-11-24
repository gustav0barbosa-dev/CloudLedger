# ADR-0005 — Saldo derivado e estorno lógico

- **Status:** aceito
- **Data:** 2026-11-24

## Contexto

RN-004, RN-006 e RN-007 tratam do cálculo de saldo e da preservação de histórico. Manter um campo `balance` mutável em `accounts` cria risco de divergência e dificulta auditoria.

## Decisão

O saldo é **derivado** em tempo de consulta:

```
saldo(conta) = OpeningBalance
             + Σ receitas confirmadas
             − Σ despesas confirmadas
             + Σ transferências recebidas confirmadas
             − Σ transferências enviadas confirmadas
```

- Apenas registros com `Status = Confirmed` entram no cálculo.
- Transferências **não** compõem receitas/despesas do consolidado (RN-006).
- Lançamentos e transferências confirmados não são excluídos: correção via `PUT` (auditada) ou `void` (estorno lógico com `VoidedAt` e `ReversalOfId` quando aplicável).
- Contas com movimentações só podem ser arquivadas (RN-014).

## Consequências

- **Positivas:** uma única fonte de verdade, impossível divergir, auditoria completa.
- **Negativas:** consultas de saldo exigem agregação; em volumes grandes pode ser necessário materializar.
- **Mitigação:** índices `(UserId, AccountId, OccurredAt)` e agregação no banco; se necessário, visão materializada no futuro.
