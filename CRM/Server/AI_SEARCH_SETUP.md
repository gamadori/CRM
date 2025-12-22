# ?? AI-Powered Ticket Search - Setup Guide

## ?? Prerequisiti

1. **Account OpenAI**
   - Crea un account su https://platform.openai.com/
   - Genera una API Key dalla sezione "API Keys"
   - Assicurati di avere credito disponibile

2. **Database Migration**
   - Applica la migration per aggiungere la colonna `DescriptionEmbedding`

---

## ?? Setup Passo-Passo

### 1?? Configurare la API Key di OpenAI

Apri `CRM\Server\appsettings.json` e sostituisci:

```json
"OpenAI": {
  "ApiKey": "sk-your-actual-openai-api-key-here",
  "EmbeddingModel": "text-embedding-3-small",
  "MaxTokens": 8191
}
```

**?? SICUREZZA**: Non committare mai la chiave API!

Per sviluppo, usa **User Secrets**:
```powershell
cd CRM\Server
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-real-key"
```

---

### 2?? Creare e Applicare la Migration

```powershell
# Dalla root del progetto
cd CRM\Server

# Crea la migration
dotnet ef migrations add AddDescriptionEmbeddingToTickets

# Applica al database
dotnet ef database update
```

**Oppure da Visual Studio Package Manager Console:**
```powershell
Add-Migration AddDescriptionEmbeddingToTickets
Update-Database
```

---

### 3?? Generare Embeddings per Ticket Esistenti

Hai **3 opzioni**:

#### **Opzione A: Script PowerShell Automatico** (CONSIGLIATO)

```powershell
# Dalla directory CRM\Server\Scripts
.\GenerateEmbeddings.ps1 -BaseUrl "https://localhost:7001" -BatchSize 10
```

Lo script:
- ? Mostra statistiche iniziali
- ? Processa ticket in batch automaticamente
- ? Gestisce errori e retry
- ? Mostra statistiche finali

#### **Opzione B: Chiamate API Manuali**

**1. Verifica stato:**
```http
GET https://localhost:7001/api/Tickets/embeddings-stats
Authorization: Bearer YOUR_JWT_TOKEN
```

**Risposta:**
```json
{
  "totalClosedTickets": 1523,
  "ticketsWithEmbedding": 0,
  "ticketsWithoutEmbedding": 1523,
  "completionPercentage": 0,
  "ready": false
}
```

**2. Genera embeddings (batch di 10):**
```http
POST https://localhost:7001/api/Tickets/generate-embeddings?batchSize=10
Authorization: Bearer YOUR_JWT_TOKEN
```

**Risposta:**
```json
{
  "message": "Elaborazione completata",
  "processed": 10,
  "errors": 0,
  "remainingTickets": 1513,
  "processingTimeMs": 2345,
  "suggestion": "Esegui nuovamente l'endpoint per processare il prossimo batch"
}
```

**3. Ripeti fino a `remainingTickets: 0`**

#### **Opzione C: Postman/Insomnia**

1. Importa questa collection:
```json
{
  "name": "Ticket Embeddings",
  "requests": [
    {
      "name": "Statistics",
      "method": "GET",
      "url": "{{baseUrl}}/api/Tickets/embeddings-stats"
    },
    {
      "name": "Generate Embeddings",
      "method": "POST",
      "url": "{{baseUrl}}/api/Tickets/generate-embeddings?batchSize=10"
    }
  ],
  "variables": {
    "baseUrl": "https://localhost:7001"
  }
}
```

---

## ?? Utilizzo della Ricerca AI

### Da UI Blazor

1. Naviga su `/Tickets/Search`
2. Inserisci descrizione problema
3. Clicca "Ricerca AI"
4. Visualizza ticket simili con percentuale match

### Da API

```http
POST https://localhost:7001/api/Tickets/semantic-search
Content-Type: application/json

{
  "problemDescription": "Il sistema non invia email dopo aggiornamento",
  "topResults": 10,
  "minSimilarityThreshold": 60.0,
  "onlyClosedTickets": true
}
```

**Risposta:**
```json
{
  "results": [
    {
      "ticketId": 1234,
      "ticketNumber": "#1234",
      "title": "Sistema email non funziona dopo update...",
      "description": "...",
      "customerName": "Acme Corp",
      "similarityPercentage": 87.5,
      "closedDate": "2024-01-15T10:30:00",
      "solution": "Riavviato servizio SMTP e aggiornato configurazione..."
    }
  ],
  "totalAnalyzed": 1523,
  "processingTimeMs": 245
}
```

---

## ?? Monitoraggio

### Statistiche

```powershell
# Controlla stato embeddings
curl https://localhost:7001/api/Tickets/embeddings-stats
```

### Log

Gli embedding vengono loggati in:
- `LogEvents` (tabella DB)
- Application Insights (se configurato)

---

## ?? Costi Stimati

**Modello**: `text-embedding-3-small`
- **Prezzo**: ~$0.02 per 1M token
- **Dimensioni embedding**: 1536 dimensioni

**Esempi**:
- 1.000 ticket = ~$0.05
- 10.000 ticket = ~$0.50
- 100.000 ticket = ~$5.00

**Ricerca**:
- Costo per ricerca: ~$0.0001 (praticamente gratuito!)

---

## ?? Troubleshooting

### Errore: "OpenAI API Key non configurata"
?? Verifica che la chiave sia in `appsettings.json` o User Secrets

### Errore: "Rate limit exceeded"
?? Riduci `batchSize` a 5 e aggiungi pause più lunghe

### Embedding null dopo chiusura ticket
?? Verifica che la migration sia stata applicata
?? Controlla i log per errori OpenAI

### Ricerca lenta
?? Assicurati che gli embeddings siano pre-calcolati
?? Controlla che il database non carichi tutti i ticket in memoria

---

## ?? Configurazione Avanzata

### Cambiare Modello Embedding

In `appsettings.json`:
```json
"OpenAI": {
  "EmbeddingModel": "text-embedding-3-large"  // Più preciso ma più costoso
}
```

### Background Job (opzionale)

Per generare embeddings automaticamente in background:
```csharp
// In Program.cs
builder.Services.AddHostedService<EmbeddingBackgroundService>();
```

---

## ?? Risorse

- [OpenAI Embeddings Documentation](https://platform.openai.com/docs/guides/embeddings)
- [Pricing Calculator](https://openai.com/pricing)
- [Best Practices](https://platform.openai.com/docs/guides/embeddings/use-cases)

---

## ? Checklist Setup

- [ ] API Key OpenAI configurata
- [ ] Migration applicata al database
- [ ] Embeddings generati per ticket esistenti
- [ ] Ricerca testata e funzionante
- [ ] Monitoraggio attivo

---

**?? Note**: 
- Gli embedding vengono generati automaticamente alla chiusura di nuovi ticket
- I ticket senza descrizione vengono ignorati
- La ricerca funziona solo su ticket chiusi con embedding
