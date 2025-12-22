# Script per generare embeddings per tutti i ticket chiusi esistenti
# Esegui questo script dopo aver applicato la migration

param(
    [string]$BaseUrl = "https://localhost:7001",
    [int]$BatchSize = 10,
    [string]$Token = "" # Inserisci qui il token JWT se necessario
)

Write-Host "?? Avvio generazione embeddings per ticket esistenti..." -ForegroundColor Cyan
Write-Host "URL: $BaseUrl" -ForegroundColor Gray
Write-Host "Batch Size: $BatchSize" -ForegroundColor Gray
Write-Host ""

# Headers
$headers = @{
    "Content-Type" = "application/json"
}

if ($Token) {
    $headers["Authorization"] = "Bearer $Token"
}

# Step 1: Ottieni statistiche iniziali
Write-Host "?? Recupero statistiche..." -ForegroundColor Yellow
try {
    $statsResponse = Invoke-RestMethod -Uri "$BaseUrl/api/Tickets/embeddings-stats" -Method GET -Headers $headers
    
    Write-Host ""
    Write-Host "=== STATISTICHE INIZIALI ===" -ForegroundColor Green
    Write-Host "Ticket chiusi totali: $($statsResponse.totalClosedTickets)" -ForegroundColor White
    Write-Host "Con embedding: $($statsResponse.ticketsWithEmbedding)" -ForegroundColor Green
    Write-Host "Senza embedding: $($statsResponse.ticketsWithoutEmbedding)" -ForegroundColor Red
    Write-Host "Senza descrizione: $($statsResponse.ticketsWithoutDescription)" -ForegroundColor Gray
    Write-Host "Completamento: $($statsResponse.completionPercentage)%" -ForegroundColor Cyan
    Write-Host ""
    
    if ($statsResponse.ready) {
        Write-Host "? Tutti i ticket hanno già gli embeddings!" -ForegroundColor Green
        exit 0
    }
}
catch {
    Write-Host "? Errore nel recupero delle statistiche: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Processa in batch fino a completamento
$iteration = 1
$totalProcessed = 0

while ($true) {
    Write-Host "?? Batch #$iteration - Processing..." -ForegroundColor Yellow
    
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/Tickets/generate-embeddings?batchSize=$BatchSize" -Method POST -Headers $headers
        
        $totalProcessed += $response.processed
        
        Write-Host "   ? Processati: $($response.processed)" -ForegroundColor Green
        
        if ($response.errors -gt 0) {
            Write-Host "   ??  Errori: $($response.errors)" -ForegroundColor Red
            foreach ($error in $response.errorDetails) {
                Write-Host "      - $error" -ForegroundColor Red
            }
        }
        
        Write-Host "   ??  Tempo: $($response.processingTimeMs)ms" -ForegroundColor Gray
        Write-Host "   ?? Rimanenti: $($response.remainingTickets)" -ForegroundColor Cyan
        Write-Host ""
        
        # Se non ci sono più ticket, esci
        if ($response.remainingTickets -eq 0) {
            Write-Host "?? COMPLETATO! Tutti i ticket hanno gli embeddings." -ForegroundColor Green
            break
        }
        
        # Pausa di 2 secondi tra i batch per non sovraccaricare OpenAI API
        Start-Sleep -Seconds 2
        
        $iteration++
    }
    catch {
        Write-Host "? Errore durante l'elaborazione del batch: $_" -ForegroundColor Red
        
        # In caso di errore, aspetta 5 secondi e riprova
        Write-Host "? Attendo 5 secondi prima di riprovare..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
}

# Step 3: Statistiche finali
Write-Host ""
Write-Host "?? Recupero statistiche finali..." -ForegroundColor Yellow

try {
    $finalStats = Invoke-RestMethod -Uri "$BaseUrl/api/Tickets/embeddings-stats" -Method GET -Headers $headers
    
    Write-Host ""
    Write-Host "=== STATISTICHE FINALI ===" -ForegroundColor Green
    Write-Host "Ticket processati totali: $totalProcessed" -ForegroundColor Cyan
    Write-Host "Ticket con embedding: $($finalStats.ticketsWithEmbedding)" -ForegroundColor Green
    Write-Host "Completamento: $($finalStats.completionPercentage)%" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "? Processo completato con successo!" -ForegroundColor Green
}
catch {
    Write-Host "??  Impossibile recuperare le statistiche finali: $_" -ForegroundColor Yellow
}
