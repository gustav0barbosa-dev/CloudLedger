# Protótipo visual

`CloudLedger_Prototipo.html` é o protótipo estático de referência do dashboard (tema escuro, roxo/ciano, layout responsivo). Ele não possui backend: os botões apenas exibem toasts.

A implementação Angular reproduz a identidade visual e evolui o protótipo para telas funcionais:

| Protótipo | Implementação |
| --- | --- |
| Visão geral | `features/dashboard` |
| Transações | `features/transactions` |
| Despesas | `features/expenses` (visão por categoria) |
| Pagamentos futuros | `features/recurring-payments` |
| Metas financeiras | `features/goals` |
| Sidebar / topbar | `layout/shell` |

Paleta de referência:

| Token | Valor |
| --- | --- |
| `--bg` | `#17132b` |
| `--panel` | `#0d0d10` |
| `--card` | `#151518` |
| `--line` | `#292930` |
| `--purple` | `#8b5cf6` |
| `--cyan` | `#42c7df` |
| `--green` | `#43e0ad` |
| `--red` | `#ff707d` |
| `--yellow` | `#f7c65e` |
