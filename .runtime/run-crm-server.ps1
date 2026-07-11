$env:ASPNETCORE_URLS = 'http://localhost:5000'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DataProtection__KeysPath = 'D:\Progetti\CRMRedg\CRM2\.runtime\DataProtectionKeys'

$logPath = 'D:\Progetti\CRMRedg\CRM2\.runtime\server.log'
"Starting CRM server at $(Get-Date -Format o)" | Out-File -FilePath $logPath -Encoding utf8

try {
    dotnet run --project 'CRM\Server\CRM.Server.csproj' --no-build --no-launch-profile *>> $logPath
    "dotnet exited with code $LASTEXITCODE at $(Get-Date -Format o)" | Out-File -FilePath $logPath -Append -Encoding utf8
}
catch {
    $_ | Out-File -FilePath $logPath -Append -Encoding utf8
}
