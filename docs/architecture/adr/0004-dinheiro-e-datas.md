# ADR-0004 — Dinheiro como decimal e datas financeiras como `DateOnly`

- **Status:** aceito
- **Data:** 2026-11-24

## Contexto

RN-001 proíbe ponto flutuante binário para dinheiro. RN-011 exige UTC para instantes e data local para datas financeiras sem horário.

## Decisão

- Valores monetários: `decimal` no C# com precisão `(18,2)`, mapeado para `numeric(18,2)` no PostgreSQL. Arredondamento `MidpointRounding.AwayFromZero` apenas na borda de apresentação; cálculos internos mantêm a precisão.
- Datas financeiras (`OccurredAt`, `DueDate`, `Deadline`): `DateOnly` ↔ `date`.
- Instantes (`CreatedAt`, `VoidedAt`, `PaidAt`, auditoria): `DateTime` UTC ↔ `timestamptz`.
- A API serializa datas como `yyyy-MM-dd` e instantes como ISO-8601 UTC (`...Z`).
- Moeda única BRL no MVP (`Currency = "BRL"`), sem conversão.

## Consequências

- **Positivas:** ausência de erros de arredondamento binário, comparações de data sem deslocamento de fuso.
- **Negativas:** suporte a múltiplas moedas exigirá evolução do modelo.
- **Mitigação:** campo `Currency` já presente em `accounts`.
