<#
.SYNOPSIS
    Teste de fumaça ponta a ponta da API do CloudLedger.

.DESCRIPTION
    Exercita autenticação, contas, categorias, lançamentos, transferências,
    pagamentos recorrentes, metas, notificações, auditoria e exportação.
    Requer a API em execução (por padrão em http://localhost:5080).

.EXAMPLE
    ./scripts/smoke-test.ps1
    ./scripts/smoke-test.ps1 -BaseUrl http://localhost:5000 -Email demo@cloudledger.dev -Password 'Demo@12345'
#>
param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$Email = "demo@cloudledger.dev",
    [string]$Password = "Demo@12345"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

$script:Passed = 0
$script:Failed = 0

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        $Body,
        [string]$Token,
        [int[]]$Expect = @(200, 201, 204)
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method      = $Method
        Uri         = "$BaseUrl$Path"
        Headers     = $headers
        ContentType = "application/json"
        TimeoutSec  = 30
        UseBasicParsing = $true
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress) }

    try {
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        $status = [int]$_.Exception.Response.StatusCode
        $content = $_.ErrorDetails.Message
    }

    if ($Expect -notcontains $status) {
        $script:Failed++
        Write-Host ("FAIL {0,-6} {1,-62} -> {2} (esperado {3})" -f $Method, $Path, $status, ($Expect -join '/')) -ForegroundColor Red
        if ($content) { Write-Host "     $content" -ForegroundColor DarkRed }
        return $null
    }

    $script:Passed++
    Write-Host ("OK   {0,-6} {1,-62} -> {2}" -f $Method, $Path, $status) -ForegroundColor Green

    if ([string]::IsNullOrWhiteSpace($content)) { return $null }
    try { return ($content | ConvertFrom-Json) } catch { return $content }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if ($Condition) {
        $script:Passed++
        Write-Host "OK   $Message" -ForegroundColor Green
    }
    else {
        $script:Failed++
        Write-Host "FAIL $Message" -ForegroundColor Red
    }
}

$today = Get-Date
$todayText = $today.ToString("yyyy-MM-dd")
$monthStart = (Get-Date -Day 1).ToString("yyyy-MM-dd")
$monthEnd = (Get-Date -Day 1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd")

Write-Host "`n== Autenticação ==" -ForegroundColor Cyan

$unauthorized = Invoke-Api -Method GET -Path "/api/v1/dashboard" -Expect @(401)
Assert-True ($null -eq $unauthorized) "Dashboard exige autenticação"

$login = Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{ email = $Email; password = $Password }
if ($null -eq $login) {
    Write-Host "`nLogin falhou; abortando." -ForegroundColor Red
    exit 1
}
$token = $login.accessToken
Assert-True (-not [string]::IsNullOrWhiteSpace($token)) "Login retorna access token"
Assert-True (-not [string]::IsNullOrWhiteSpace($login.refreshToken)) "Login retorna refresh token"

$me = Invoke-Api -Method GET -Path "/api/v1/users/me" -Token $token
Assert-True ($me.email -eq $Email) "Perfil do usuário autenticado"

Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{ email = "invalido"; password = "" } -Expect @(400) | Out-Null

$refreshed = Invoke-Api -Method POST -Path "/api/v1/auth/refresh" -Body @{ refreshToken = $login.refreshToken }
if ($refreshed) {
    Assert-True (-not [string]::IsNullOrWhiteSpace($refreshed.accessToken)) "Refresh emite novo access token"
    $token = $refreshed.accessToken
}

Write-Host "`n== Leitura inicial ==" -ForegroundColor Cyan

$dashboard = Invoke-Api -Method GET -Path "/api/v1/dashboard" -Token $token
Assert-True ($null -ne $dashboard.summary) "Dashboard retorna resumo"
Assert-True ($dashboard.series.Count -gt 0) "Dashboard retorna série temporal"

$accounts = Invoke-Api -Method GET -Path "/api/v1/accounts" -Token $token
Assert-True ($accounts.Count -gt 0) "Lista de contas não vazia"

$categories = Invoke-Api -Method GET -Path "/api/v1/categories" -Token $token
Assert-True ($categories.Count -gt 0) "Lista de categorias não vazia"

Invoke-Api -Method GET -Path "/api/v1/transactions?page=1&pageSize=5" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/transfers?page=1&pageSize=5" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/recurring-payments?activeOnly=true" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/goals" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/notifications?page=1&pageSize=5" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/notifications/unread-count" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/audit-events?page=1&pageSize=5" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/dashboard/expenses-by-category" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/dashboard/upcoming-payments" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/dashboard/goals" -Token $token | Out-Null

Write-Host "`n== Contas ==" -ForegroundColor Cyan

$account = Invoke-Api -Method POST -Path "/api/v1/accounts" -Token $token -Body @{
    name           = "Conta E2E"
    type           = "Checking"
    openingBalance = 500.00
    currency       = "BRL"
}
Assert-True ($account.balance -eq 500.00) "Conta criada com saldo inicial de 500,00"

$updatedAccount = Invoke-Api -Method PUT -Path "/api/v1/accounts/$($account.id)" -Token $token -Body @{
    name           = "Conta E2E Renomeada"
    type           = "Savings"
    openingBalance = 750.00
}
Assert-True ($updatedAccount.balance -eq 750.00) "Conta atualizada com novo saldo inicial"

Invoke-Api -Method POST -Path "/api/v1/accounts" -Token $token -Body @{ name = ""; type = "Checking"; openingBalance = 0 } -Expect @(400) | Out-Null

Invoke-Api -Method POST -Path "/api/v1/accounts/$($account.id)/archive" -Token $token | Out-Null
Invoke-Api -Method POST -Path "/api/v1/accounts/$($account.id)/unarchive" -Token $token | Out-Null

Write-Host "`n== Categorias ==" -ForegroundColor Cyan

$category = Invoke-Api -Method POST -Path "/api/v1/categories" -Token $token -Body @{
    name  = "Categoria E2E"
    kind  = "Expense"
    color = "#FF8800"
    icon  = "tag"
}
$updatedCategory = Invoke-Api -Method PUT -Path "/api/v1/categories/$($category.id)" -Token $token -Body @{
    name  = "Categoria E2E Editada"
    color = "#0088FF"
    icon  = "tag"
}
Assert-True ($updatedCategory.name -eq "Categoria E2E Editada") "Categoria atualizada"

Write-Host "`n== Lançamentos ==" -ForegroundColor Cyan

$transaction = Invoke-Api -Method POST -Path "/api/v1/transactions" -Token $token -Body @{
    accountId   = $account.id
    categoryId  = $category.id
    type        = "Expense"
    amount      = 123.45
    description = "Despesa E2E"
    occurredAt  = $todayText
}
Assert-True ($transaction.status -eq "Confirmed") "Lançamento criado como confirmado"

$updatedTransaction = Invoke-Api -Method PUT -Path "/api/v1/transactions/$($transaction.id)" -Token $token -Body @{
    accountId   = $account.id
    categoryId  = $category.id
    type        = "Expense"
    amount      = 100.00
    description = "Despesa E2E editada"
    occurredAt  = $todayText
}
Assert-True ($updatedTransaction.amount -eq 100.00) "Lançamento atualizado"

$accountAfterTransaction = Invoke-Api -Method GET -Path "/api/v1/accounts/$($account.id)" -Token $token
Assert-True ($accountAfterTransaction.balance -eq 650.00) "Saldo considera despesa confirmada (750 - 100)"

Invoke-Api -Method POST -Path "/api/v1/transactions/$($transaction.id)/void" -Token $token | Out-Null

$accountAfterVoid = Invoke-Api -Method GET -Path "/api/v1/accounts/$($account.id)" -Token $token
Assert-True ($accountAfterVoid.balance -eq 750.00) "Estorno devolve o valor ao saldo"

$voided = Invoke-Api -Method GET -Path "/api/v1/transactions/$($transaction.id)" -Token $token
Assert-True ($voided.status -eq "Voided") "Lançamento permanece com status estornado"

Invoke-Api -Method POST -Path "/api/v1/transactions" -Token $token -Body @{
    accountId = $account.id; type = "Expense"; amount = 0; description = ""; occurredAt = $todayText
} -Expect @(400, 422) | Out-Null

Write-Host "`n== Transferências ==" -ForegroundColor Cyan

$destination = $accounts | Where-Object { $_.id -ne $account.id } | Select-Object -First 1
$transfer = Invoke-Api -Method POST -Path "/api/v1/transfers" -Token $token -Body @{
    sourceAccountId      = $account.id
    destinationAccountId = $destination.id
    amount               = 50.00
    occurredAt           = $todayText
    description          = "Transferência E2E"
}
Assert-True ($transfer.status -eq "Confirmed") "Transferência criada"

$sourceAfterTransfer = Invoke-Api -Method GET -Path "/api/v1/accounts/$($account.id)" -Token $token
Assert-True ($sourceAfterTransfer.balance -eq 700.00) "Saldo de origem reduzido (750 - 50)"

Invoke-Api -Method POST -Path "/api/v1/transfers/$($transfer.id)/void" -Token $token | Out-Null
$sourceAfterVoid = Invoke-Api -Method GET -Path "/api/v1/accounts/$($account.id)" -Token $token
Assert-True ($sourceAfterVoid.balance -eq 750.00) "Estorno da transferência devolve o saldo"

Write-Host "`n== Pagamentos recorrentes ==" -ForegroundColor Cyan

$recurring = Invoke-Api -Method POST -Path "/api/v1/recurring-payments" -Token $token -Body @{
    accountId   = $account.id
    categoryId  = $category.id
    description = "Assinatura E2E"
    amount      = 29.90
    frequency   = "Monthly"
    nextDueDate = $monthStart
}
Assert-True ($recurring.isActive) "Pagamento recorrente ativo"

$occurrences = Invoke-Api -Method GET -Path "/api/v1/recurring-payments/$($recurring.id)/occurrences?from=$monthStart&to=$monthEnd" -Token $token
Assert-True (@($occurrences).Count -ge 1) "Ocorrências projetadas"

$first = $occurrences | Where-Object { $_.status -eq "Pending" } | Select-Object -First 1
$paid = Invoke-Api -Method POST -Path "/api/v1/recurring-payments/$($recurring.id)/occurrences/$($first.dueDate)/pay" -Token $token
Assert-True ($paid.status -eq "Paid") "Ocorrência marcada como paga"

Invoke-Api -Method POST -Path "/api/v1/recurring-payments/$($recurring.id)/occurrences/$($first.dueDate)/pay" -Token $token -Expect @(409) | Out-Null

$nextPending = (Invoke-Api -Method GET -Path "/api/v1/recurring-payments/$($recurring.id)/occurrences?from=$monthStart&to=$((Get-Date -Day 1).AddMonths(4).AddDays(-1).ToString('yyyy-MM-dd'))" -Token $token) |
    Where-Object { $_.status -eq "Pending" } | Select-Object -First 1
Invoke-Api -Method POST -Path "/api/v1/recurring-payments/$($recurring.id)/occurrences/$($nextPending.dueDate)/skip" -Token $token | Out-Null

Write-Host "`n== Metas ==" -ForegroundColor Cyan

$goal = Invoke-Api -Method POST -Path "/api/v1/goals" -Token $token -Body @{
    name         = "Meta E2E"
    targetAmount = 1000.00
    deadline     = (Get-Date).AddMonths(6).ToString("yyyy-MM-dd")
}
Assert-True ($goal.progressPercent -eq 0) "Meta criada sem progresso"

$movement = Invoke-Api -Method POST -Path "/api/v1/goals/$($goal.id)/movements" -Token $token -Body @{
    amount     = 250.00
    type       = "Deposit"
    occurredAt = $todayText
}
Assert-True ($movement.amount -eq 250.00) "Aporte registrado"

$goalAfter = Invoke-Api -Method GET -Path "/api/v1/goals/$($goal.id)" -Token $token
Assert-True ($goalAfter.accumulated -eq 250.00) "Meta acumula o aporte"
Assert-True ($goalAfter.progressPercent -eq 25) "Progresso da meta em 25%"

$movements = Invoke-Api -Method GET -Path "/api/v1/goals/$($goal.id)/movements" -Token $token
Assert-True (@($movements).Count -eq 1) "Movimentações da meta listadas"

Invoke-Api -Method POST -Path "/api/v1/goals/$($goal.id)/cancel" -Token $token | Out-Null

Write-Host "`n== Relatórios ==" -ForegroundColor Cyan

$report = Invoke-Api -Method POST -Path "/api/v1/reports/transactions/export?from=$monthStart&to=$monthEnd" -Token $token
Assert-True (-not [string]::IsNullOrWhiteSpace($report.fileName)) "Exportação gera nome de arquivo"

if ($report.url -like "/*") {
    try {
        $file = Invoke-WebRequest -Uri "$BaseUrl$($report.url)" -UseBasicParsing -TimeoutSec 20
        Assert-True ($file.StatusCode -eq 200 -and $file.RawContentLength -gt 0) "Arquivo exportado acessível em $($report.url)"
    }
    catch {
        Assert-True $false "Arquivo exportado acessível em $($report.url)"
    }
}

Write-Host "`n== Notificações e auditoria ==" -ForegroundColor Cyan

$notifications = Invoke-Api -Method GET -Path "/api/v1/notifications?page=1&pageSize=10" -Token $token
if ($notifications.items.Count -gt 0) {
    Invoke-Api -Method POST -Path "/api/v1/notifications/$($notifications.items[0].id)/read" -Token $token | Out-Null
}
Invoke-Api -Method POST -Path "/api/v1/notifications/read-all" -Token $token | Out-Null

$audit = Invoke-Api -Method GET -Path "/api/v1/audit-events?page=1&pageSize=50" -Token $token
Assert-True ($audit.items.Count -gt 0) "Trilha de auditoria registrada"
$actions = @($audit.items | ForEach-Object { $_.action })
Assert-True ($actions -contains "UserLoggedIn") "Auditoria contém login"

Write-Host "`n== Cadastro e logout ==" -ForegroundColor Cyan

$newEmail = "e2e-$([Guid]::NewGuid().ToString('N').Substring(0,8))@cloudledger.dev"
$registered = Invoke-Api -Method POST -Path "/api/v1/auth/register" -Body @{
    name     = "Usuário E2E"
    email    = $newEmail
    password = "Senha@12345"
}
Assert-True ($registered.user.email -eq $newEmail) "Cadastro de novo usuário"

Invoke-Api -Method POST -Path "/api/v1/auth/register" -Body @{
    name = "Duplicado"; email = $newEmail; password = "Senha@12345"
} -Expect @(409) | Out-Null

Invoke-Api -Method POST -Path "/api/v1/auth/logout" -Token $registered.accessToken -Body @{ refreshToken = $registered.refreshToken } | Out-Null
Invoke-Api -Method POST -Path "/api/v1/auth/refresh" -Body @{ refreshToken = $registered.refreshToken } -Expect @(401) | Out-Null

Write-Host "`n== Resultado ==" -ForegroundColor Cyan
Write-Host ("Verificações OK: {0}" -f $script:Passed) -ForegroundColor Green
if ($script:Failed -gt 0) {
    Write-Host ("Falhas: {0}" -f $script:Failed) -ForegroundColor Red
    exit 1
}

Write-Host "Todos os testes de fumaça passaram." -ForegroundColor Green
exit 0
