param(
    [Parameter(Mandatory = $true)]
    [string]$BotToken,

    [Parameter(Mandatory = $true)]
    [string]$PublicBaseUrl,

    [Parameter(Mandatory = $false)]
    [string]$WebhookSecret = "",

    [switch]$Delete,

    [switch]$DropPendingUpdates
)

$ErrorActionPreference = "Stop"

function Invoke-TelegramApi {
    param(
        [Parameter(Mandatory = $true)]
        [string]$MethodName,

        [Parameter(Mandatory = $false)]
        [string]$BodyJson = ""
    )

    $uri = "https://api.telegram.org/bot$BotToken/$MethodName"
    if ([string]::IsNullOrWhiteSpace($BodyJson)) {
        return Invoke-RestMethod -Method Post -Uri $uri
    }

    return Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $BodyJson
}

$baseUrl = $PublicBaseUrl.Trim().TrimEnd("/")
if (-not $baseUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "PublicBaseUrl must start with https://"
}

if ($Delete) {
    $query = ""
    if ($DropPendingUpdates.IsPresent) {
        $query = "?drop_pending_updates=true"
    }

    $deleteUri = "https://api.telegram.org/bot$BotToken/deleteWebhook$query"
    $deleteResult = Invoke-RestMethod -Method Post -Uri $deleteUri

    Write-Host "deleteWebhook result:" -ForegroundColor Cyan
    $deleteResult | ConvertTo-Json -Depth 10

    Write-Host ""
    Write-Host "getWebhookInfo:" -ForegroundColor Cyan
    $info = Invoke-RestMethod -Method Get -Uri "https://api.telegram.org/bot$BotToken/getWebhookInfo"
    $info | ConvertTo-Json -Depth 10
    exit 0
}

$webhookUrl = "$baseUrl/api/telegram/webhook"
$payload = @{
    url = $webhookUrl
}

if (-not [string]::IsNullOrWhiteSpace($WebhookSecret)) {
    $payload.secret_token = $WebhookSecret
}

$body = $payload | ConvertTo-Json -Depth 5
$setResult = Invoke-TelegramApi -MethodName "setWebhook" -BodyJson $body

Write-Host "setWebhook result:" -ForegroundColor Green
$setResult | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "getWebhookInfo:" -ForegroundColor Cyan
$infoResult = Invoke-RestMethod -Method Get -Uri "https://api.telegram.org/bot$BotToken/getWebhookInfo"
$infoResult | ConvertTo-Json -Depth 10
