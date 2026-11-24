# Especificação do MVP — CloudLedger

Versão 1.0 · derivada do *Relatório de visão do produto, requisitos e arquitetura* (24/11/2026).

## 1. Objetivo

Permitir que uma pessoa organize contas, receitas, despesas, transferências simuladas, pagamentos recorrentes e metas de economia, acompanhando a situação financeira em um dashboard, com dados isolados por usuário.

## 2. Decisões de escopo confirmadas

| # | Decisão em aberto | Decisão para o MVP |
| --- | --- | --- |
| 1 | Uso pessoal ou familiar | Multiusuário: cada usuário vê apenas os próprios dados |
| 2 | Moeda | BRL única (`Currency = "BRL"`), sem conversão |
| 3 | Saldo inicial | Editável apenas na criação/edição da conta (com auditoria) |
| 4 | Recorrências | Somente previstas; geração de lançamento apenas ao marcar ocorrência como paga |
| 5 | Autenticação | Própria, JWT de curta duração + refresh token, hash de senha com BCrypt |
| 6 | IaC | AWS CDK (TypeScript), documentada em `infrastructure/` |
| 7 | Hospedagem | App Runner/ECS para API, S3 + CloudFront para SPA, RDS PostgreSQL |

## 3. Casos de uso principais

### UC-01 — Cadastro e autenticação
- **Ator:** visitante.
- **Fluxo:** informa nome, e-mail e senha → backend valida unicidade do e-mail, aplica política de senha e persiste hash → retorna tokens → SPA armazena o access token em memória e o refresh token em cookie/armazenamento seguro.
- **Exceções:** e-mail já cadastrado (409), credenciais inválidas (401), payload inválido (400).

### UC-02 — Gestão de contas
- **Ator:** usuário autenticado.
- **Fluxo:** cria conta com nome, tipo, saldo inicial → consulta, edita e arquiva. Conta com movimentações não é excluída fisicamente (RN-014); é arquivada.

### UC-03 — Lançamentos
- **Fluxo:** registra receita ou despesa com valor > 0, data, descrição, conta e categoria. O saldo é derivado (RN-004). Lançamento confirmado pode ser estornado (status `Voided`), preservando trilha.
- **Exceções:** valor ≤ 0, conta/categoria de outro usuário, conta arquivada, categoria incompatível com o tipo.

### UC-04 — Transferência simulada
- **Fluxo:** seleciona conta origem, destino (distintas, mesmo usuário), valor e data → operação atômica que debita e credita. Não compõe receitas/despesas (RN-006).

### UC-05 — Pagamentos recorrentes
- **Fluxo:** cria recorrência (valor, periodicidade, próxima data, conta, categoria) → sistema projeta ocorrências futuras → usuário marca ocorrência como paga → gera **um** lançamento de despesa para aquela data (RN-008/RN-009), de forma idempotente.

### UC-06 — Metas financeiras
- **Fluxo:** cria meta (título, valor-alvo, prazo opcional, conta opcional) → registra aportes/retiradas → progresso = aportes − retiradas, preservando o valor real acumulado e limitando o percentual visual entre 0% e 100% (RN-010).

### UC-07 — Dashboard
- **Fluxo:** seleciona período → consulta resumo (saldo total, receitas, despesas, economia), série temporal diária/mensal, despesas por categoria, transações recentes, próximos pagamentos e metas.

### UC-08 — Auditoria e notificações
- **Fluxo:** eventos relevantes (cadastro, criação/alteração/estorno de lançamento, transferência, pagamento de ocorrência, aporte em meta) são registrados no MongoDB e podem ser consultados pelo próprio usuário. Notificações internas sinalizam pagamentos próximos e metas atingidas.

## 4. Requisitos funcionais (rastreamento)

| ID | Requisito | Implementação |
| --- | --- | --- |
| RF-001 | Cadastro nome/e-mail/senha | `POST /api/v1/auth/register` |
| RF-002 | Autenticação e logout | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` |
| RF-003 | Consulta/edição de perfil | `GET/PUT /users/me` |
| RF-004 | Proteção por identidade e propriedade | JWT + filtro de propriedade em toda consulta |
| RF-010..014 | Dashboard | `GET /dashboard/summary`, `/dashboard/recent-transactions`, `/dashboard/upcoming-payments` |
| RF-020 | CRUD/arquivamento de contas | `/accounts` |
| RF-021 | Receitas e despesas | `POST /transactions` |
| RF-022 | Transferência simulada | `POST /transfers` |
| RF-023 | Filtros e paginação | `GET /transactions?from&to&type&categoryId&accountId&q&page&pageSize` |
| RF-024 | Estorno lógico | `POST /transactions/{id}/void` |
| RF-030 | Categorias padrão e personalizadas | seed + `/categories` |
| RF-031..033 | Recorrências | `/recurring-payments`, `POST /{id}/occurrences/{date}/pay` |
| RF-040..042 | Metas | `/goals`, `/goals/{id}/movements` |
| RF-050..052 | Auditoria e notificações | MongoDB `audit_events`, `/audit-events`, `/notifications` |

## 5. Regras de negócio

As regras RN-001 a RN-014 do documento de origem estão implementadas no domínio:

- **RN-001** — `decimal(18,2)` no PostgreSQL; nunca `double`/`float`.
- **RN-002** — valores > 0 validados no DTO e nas entidades.
- **RN-003** — `UserId` derivado do token; toda leitura/escrita filtra pelo proprietário.
- **RN-004** — `saldo = OpeningBalance + receitas confirmadas − despesas confirmadas + transferências recebidas − transferências enviadas`.
- **RN-005** — transferência exige contas distintas do mesmo usuário; operação transacional.
- **RN-006** — transferências não entram nos totais de receita/despesa.
- **RN-007** — lançamentos não são excluídos fisicamente; estorno lógico auditável.
- **RN-008** — índice único `(RecurringPaymentId, DueDate)` garante uma ocorrência por data e um único lançamento.
- **RN-009** — recorrência só gera despesa ao ser marcada como paga.
- **RN-010** — progresso = aportes − retiradas; percentual visual limitado a [0, 100]; valor real preservado.
- **RN-011** — `DateOnly` para datas financeiras; `DateTime` UTC para instantes (CreatedAt, OccurredAt de auditoria).
- **RN-012** — consumidores idempotentes via DynamoDB.
- **RN-013** — recurso de outro usuário retorna 404 (não revela existência).
- **RN-014** — conta com movimentações só pode ser arquivada.

## 6. Critérios de aceite do MVP

- [x] Cadastro, login e acesso somente aos próprios dados.
- [x] Contas e categorias cadastradas, receitas/despesas lançadas, histórico paginado.
- [x] Saldo e indicadores do dashboard conferem com os lançamentos.
- [x] Meta criada com aportes e progresso.
- [x] Interface responsiva com estados vazio, carregando e erro.
- [x] Testes automatizados dos fluxos críticos + OpenAPI publicado.
- [x] Ambiente local reproduzível pelo README, sem segredos versionados.

## 7. Fora de escopo

Integrações bancárias, pagamentos reais, múltiplas moedas, open finance, aplicativo móvel nativo, criptografia própria e acesso de administrador ao conteúdo financeiro pessoal.
