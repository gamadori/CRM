# ✅ STANDALONE VAPID Key Generator - genera chiavi VERE per Push Notifications
# ESEGUI: .\GenerateVapidKeys_STANDALONE.ps1

Write-Host ""
Write-Host "🔐 VAPID Key Generator per CRM Push Notifications" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Controlla se dotnet-script è installato
$scriptInstalled = dotnet tool list -g | Select-String "dotnet-script"

if (-not $scriptInstalled) {
    Write-Host "⚠️  dotnet-script non trovato. Installazione in corso..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-script
    Write-Host "✅ dotnet-script installato!" -ForegroundColor Green
    Write-Host ""
}

# Crea script temporaneo inline con versione CORRETTA del package
$scriptContent = @'
#r "nuget: WebPush-NetCore, 1.0.2"
using System;
using WebPush;

var vapidKeys = VapidHelper.GenerateVapidKeys();

Console.WriteLine("======================================");
Console.WriteLine("  🔐 VAPID KEYS GENERATE SUCCESS!");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("📋 Copia queste chiavi in CRM/Server/appsettings.PushNotifications.json:");
Console.WriteLine();
Console.WriteLine("{");
Console.WriteLine("  ""PushNotifications"": {");
Console.WriteLine("    ""WebPush"": {");
Console.WriteLine("      ""subject"": ""mailto:info@a-plusautomation.com"",");
Console.WriteLine($"      ""publicKey"": ""{vapidKeys.PublicKey}"",");
Console.WriteLine($"      ""privateKey"": ""{vapidKeys.PrivateKey}""");
Console.WriteLine("    }");
Console.WriteLine("  }");
Console.WriteLine("}");
Console.WriteLine();
Console.WriteLine("⚠️  ATTENZIONE: La chiave PRIVATA va tenuta SEGRETA!");
Console.WriteLine("     NON committare su Git se il repository è pubblico.");
Console.WriteLine();
Console.WriteLine("✅ Dopo aver copiato le chiavi, riavvia il server CRM.");
Console.WriteLine();
'@

# Salva script temporaneo
$tempScript = [System.IO.Path]::GetTempFileName() + ".csx"
$scriptContent | Out-File -FilePath $tempScript -Encoding UTF8

Write-Host "🔑 Generazione VAPID keys in corso..." -ForegroundColor Yellow
Write-Host ""

# Esegui script
dotnet script $tempScript

# Rimuovi script temporaneo
Remove-Item $tempScript -Force

Write-Host ""
Write-Host "✅ FATTO!" -ForegroundColor Green
Write-Host ""

# Pausa per permettere di leggere l'output
Read-Host "Premi INVIO per chiudere"
