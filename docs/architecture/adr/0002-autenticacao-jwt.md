# ADR-0002 — Autenticação própria com JWT e refresh token

- **Status:** aceito
- **Data:** 2026-11-24

## Contexto

Era necessário escolher entre um provedor gerenciado (Cognito/Auth0) e autenticação própria. O objetivo do projeto inclui demonstrar implementação de autenticação/autorização em ASP.NET Core. Nunca se deve implementar criptografia própria.

## Decisão

- Autenticação própria com **ASP.NET Core JWT Bearer**.
- Senhas com **BCrypt** (`BCrypt.Net-Next`, work factor 12) — algoritmo consolidado, com salt por senha.
- **Access token** JWT HS256 de 60 minutos com claims `sub`, `email`, `name`, `jti`.
- **Refresh token** opaco (64 bytes aleatórios), persistido apenas como SHA-256 em `refresh_tokens`, com rotação a cada uso e revogação no logout.
- `UserId` é sempre derivado do token; nenhum endpoint aceita `userId` do cliente.
- Rate limiting nos endpoints de autenticação.

## Consequências

- **Positivas:** controle total, sem custo de provedor, demonstrável didaticamente.
- **Negativas:** responsabilidade sobre revogação, rotação e proteção de segredos é nossa.
- **Mitigação:** refresh tokens hasheados, rotação, revogação explícita, segredo forte obrigatório em produção e testes de autorização.
