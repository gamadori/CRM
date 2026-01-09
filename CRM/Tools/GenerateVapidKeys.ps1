# ? Script PowerShell per generare VAPID keys per Web Push Notifications
# Da eseguire UNA VOLTA per generare le chiavi da inserire in appsettings.json

Write-Host "?? VAPID Key Generator per CRM Push Notifications" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Verifica se WebPush-NetCore è installato
$packageCheck = dotnet list CRM\Server\CRM.Server.csproj package | Select-String "WebPush-NetCore"

if (-not $packageCheck) {
    Write-Host "??  Installo WebPush-NetCore..." -ForegroundColor Yellow
    dotnet add CRM\Server\CRM.Server.csproj package WebPush-NetCore
    Write-Host "? WebPush-NetCore installato!" -ForegroundColor Green
    Write-Host ""
}

# Esegui il tool di generazione chiavi
Write-Host "?? Generazione VAPID keys..." -ForegroundColor Yellow
Write-Host ""

dotnet run --project CRM\Tools\VapidKeyGenerator.cs

Write-Host ""
Write-Host "? FATTO! Copia le chiavi generate in appsettings.PushNotifications.json" -ForegroundColor Green
Write-Host ""
