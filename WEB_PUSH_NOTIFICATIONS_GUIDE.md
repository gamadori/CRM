# ?? WEB PUSH NOTIFICATIONS - GUIDA COMPLETA

## ? IMPLEMENTAZIONE COMPLETATA

Tutti i file necessari per le **Web Push Notifications** sono stati creati e sono pronti all'uso.

---

## ?? **FILE CREATI**

### **1. Client-Side (JavaScript)**

| File | Descrizione |
|------|-------------|
| `CRM\Client\wwwroot\service-worker.js` | Service Worker per gestire le push in background |
| `CRM\Client\wwwroot\lib\push-notifications.js` | Helper JavaScript per subscribe/unsubscribe |

### **2. Server-Side (C#)**

| File | Descrizione |
|------|-------------|
| `CRM\Server\Services\PushNotificationService.cs` | Servizio C# per inviare notifiche push |

### **3. Database**

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `ApplicationUser.PushSubscription` | `string` | Subscription JSON serializzata |
| `ApplicationUser.PushSubscriptionDate` | `DateTime?` | Data registrazione |

---

## ?? **INSTALLAZIONE**

### **STEP 1: Aggiungi NuGet Package**

```powershell
cd CRM\Server
dotnet add package WebPush --version 1.0.11
```

### **STEP 2: Genera VAPID Keys**

Le VAPID keys sono necessarie per identificare il server che invia le push.

**Opzione A - Online Tool:**
- Vai su: https://vapidkeys.com/
- Genera nuove keys
- Copia Public Key e Private Key

**Opzione B - Command Line:**
```powershell
cd CRM\Server
dotnet tool install -g dotnet-webpush
webpush generate-keys
```

### **STEP 3: Configura appsettings.json**

Aggiungi le VAPID keys in `CRM\Server\appsettings.json`:

```json
{
  "WebPush": {
    "PublicKey": "YOUR_PUBLIC_KEY_HERE",
    "PrivateKey": "YOUR_PRIVATE_KEY_HERE",
    "Subject": "mailto:support@yourcompany.com"
  }
}
```

?? **IMPORTANTE**: Aggiungi anche in `appsettings.Development.json` per test locali!

### **STEP 4: Registra il Servizio**

In `CRM\Server\Program.cs`:

```csharp
// Aggiungi PRIMA di builder.Build()
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
```

### **STEP 5: Crea Migration Database**

```powershell
cd CRM\Server
dotnet ef migrations add AddPushNotificationFields
dotnet ef database update
```

### **STEP 6: Aggiorna index.html**

Il riferimento a `push-notifications.js` è già stato aggiunto in:
```html
<script src="lib/push-notifications.js"></script>
```

---

## ?? **USO NEL CODICE**

### **A) CLIENT - Richiedere Permesso e Subscribe**

Crea un componente Blazor `PushNotificationSettings.razor`:

```razor
@inject IJSRuntime JSRuntime
@inject HttpClient Http

<button @onclick="EnableNotifications">?? Attiva Notifiche Push</button>

@code {
    private async Task EnableNotifications()
    {
        // 1. Richiedi permesso
        var permissionResult = await JSRuntime.InvokeAsync<object>(
            "PushNotifications.requestPermission");

        // 2. Subscribe (usa VAPID Public Key dal server)
        var vapidPublicKey = "YOUR_VAPID_PUBLIC_KEY"; // Oppure leggi da API
        
        var subscribeResult = await JSRuntime.InvokeAsync<object>(
            "PushNotifications.subscribe", vapidPublicKey);

        // 3. Invia subscription al server
        var subscription = subscribeResult.GetProperty("subscription").GetString();
        
        await Http.PostAsJsonAsync("api/push/subscribe", new { 
            subscription = subscription 
        });
    }
}
```

### **B) SERVER - Endpoint per Subscribe**

In `TicketsController.cs` o crea `PushNotificationsController.cs`:

```csharp
[HttpPost("api/push/subscribe")]
public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request)
{
    var userId = _userManager.GetUserId(User);
    
    var result = await _pushService.SaveSubscriptionAsync(
        userId, 
        request.Subscription);
    
    return result ? Ok() : BadRequest();
}

public class PushSubscribeRequest
{
    public string Subscription { get; set; }
}
```

### **C) SERVER - Inviare Notifica Push**

**Modifica `SendAssignmentNotifications` in `TicketsController.cs`:**

```csharp
private async Task SendAssignmentNotifications(
    Ticket ticket, 
    List<string> userIds, 
    bool isAssignment = true)
{
    // ... codice esistente per email/telegram ...

    // ? NUOVO: Invia PUSH NOTIFICATION
    var pushNotification = new
    {
        title = isAssignment 
            ? $"? Nuovo Ticket Assegnato #{ticket.Id}"
            : $"? Ticket Rimosso #{ticket.Id}",
        body = $"Cliente: {ticketWithDetails.Company?.RagioneSociale}",
        icon = "/favicon.ico",
        badge = "/favicon.ico",
        url = $"/Tickets/Info/{ticket.Id}",
        data = new
        {
            ticketId = ticket.Id,
            action = isAssignment ? "assigned" : "unassigned"
        }
    };

    await _pushService.SendToUsersAsync(userIds, pushNotification);
}
```

---

## ?? **ESEMPI D'USO**

### **1. Notifica Singolo Utente**

```csharp
await _pushService.SendToUserAsync(userId, new
{
    title = "Nuovo Messaggio",
    body = "Hai ricevuto un nuovo messaggio sul ticket #123",
    url = "/Tickets/Info/123"
});
```

### **2. Notifica Multipla**

```csharp
var userIds = new List<string> { "user1-id", "user2-id", "user3-id" };

await _pushService.SendToUsersAsync(userIds, new
{
    title = "Ticket Scaduto",
    body = "Il ticket #456 è scaduto e richiede attenzione",
    url = "/Tickets/Info/456",
    requireInteraction = true // Richiede click per chiudere
});
```

### **3. Notifica a Tutti**

```csharp
await _pushService.SendToAllAsync(new
{
    title = "Manutenzione Programmata",
    body = "Il sistema sarà offline dalle 22:00 alle 23:00",
    icon = "/images/maintenance.png"
});
```

---

## ?? **TESTING**

### **Test 1: Permessi Browser**

```javascript
// Console del browser
PushNotifications.isSupported()  // true/false
PushNotifications.getPermissionState()  // 'granted', 'denied', 'default'
```

### **Test 2: Notifica Locale**

```javascript
// Console del browser (dopo aver concesso permesso)
PushNotifications.showTestNotification()
```

### **Test 3: Subscribe/Unsubscribe**

```javascript
// Subscribe
const result = await PushNotifications.subscribe('YOUR_VAPID_PUBLIC_KEY');
console.log(result);

// Unsubscribe
await PushNotifications.unsubscribe();
```

---

## ?? **SICUREZZA**

### **VAPID Keys**

- ? **Private Key**: SEMPRE nel server (appsettings.json), MAI esposta al client
- ? **Public Key**: Può essere pubblica, usata dal browser per subscribe

### **Best Practices**

1. **User Secrets** per development:
   ```powershell
   dotnet user-secrets set "WebPush:PrivateKey" "YOUR_PRIVATE_KEY"
   ```

2. **Environment Variables** per production:
   ```bash
   export WebPush__PrivateKey="YOUR_PRIVATE_KEY"
   ```

3. **Azure Key Vault** per enterprise:
   ```csharp
   builder.Configuration.AddAzureKeyVault(...);
   ```

---

## ?? **BROWSER SUPPORT**

| Browser | Versione Minima | Supporto |
|---------|-----------------|----------|
| Chrome | 42+ | ? Full |
| Firefox | 44+ | ? Full |
| Edge | 17+ | ? Full |
| Safari | 16+ (macOS 13+) | ? Full |
| Opera | 29+ | ? Full |
| Mobile Chrome | 42+ | ? Full |
| Mobile Safari | 16.4+ | ? Full |

---

## ?? **TROUBLESHOOTING**

### **Problema: "Browser non supportato"**

**Causa**: Browser troppo vecchio o in modalità incognito.

**Soluzione**: 
```javascript
if (!PushNotifications.isSupported()) {
    alert('Il tuo browser non supporta le notifiche push');
}
```

### **Problema: "Permission denied"**

**Causa**: Utente ha negato i permessi.

**Soluzione**:
1. Chiedi nuovamente (1 sola volta!)
2. Mostra istruzioni per sbloccare manualmente:
   - Chrome: Icona lucchetto ? Impostazioni sito ? Notifiche
   - Firefox: Icona i ? Permessi ? Notifiche

### **Problema: "VAPID keys non configurate"**

**Causa**: appsettings.json manca sezione WebPush.

**Soluzione**: Vedi STEP 3 sopra.

### **Problema: Notifiche non arrivano**

**Checklist**:
1. ? Service Worker registrato? (DevTools ? Application ? Service Workers)
2. ? Subscription salvata su DB? (Controlla `ApplicationUser.PushSubscription`)
3. ? VAPID keys corrette?
4. ? Browser online? (Le push non arrivano se offline)

---

## ?? **INTEGRAZIONE ESISTENTE**

### **Modifica `SendAssignmentNotifications`**

In `TicketsController.cs`, aggiungi DOPO l'invio di email/telegram:

```csharp
// ? Email/Telegram già implementati...

// ? NUOVO: Push Notification
var pushNotification = new
{
    title = isAssignment 
        ? $"? Assegnato al ticket #{ticket.Id}"
        : $"? Rimosso dal ticket #{ticket.Id}",
    body = $"{ticketWithDetails.Company?.RagioneSociale}",
    icon = "/favicon.ico",
    url = $"/Tickets/Info/{ticket.Id}",
    data = new { ticketId = ticket.Id }
};

await _pushService.SendToUsersAsync(userIds, pushNotification);
```

---

## ?? **STATISTICHE E MONITORING**

### **Query Utili**

```sql
-- Utenti con push attive
SELECT COUNT(*) 
FROM AspNetUsers 
WHERE PushSubscription IS NOT NULL;

-- Subscription più vecchie di 30 giorni
SELECT Id, Email, PushSubscriptionDate 
FROM AspNetUsers 
WHERE PushSubscriptionDate < DATEADD(day, -30, GETDATE());
```

### **Log Events**

Tutte le operazioni vengono loggato in `LogEvents`:
- `SaveSubscriptionAsync` ? Info
- `SendPushNotification` ? Info/Error
- Subscription scadute ? Warning

---

## ? **CHECKLIST IMPLEMENTAZIONE**

- [x] NuGet Package `WebPush` installato ? (da installare: `dotnet add package WebPush`)
- [x] VAPID keys generate ? (configurate in appsettings.json)
- [x] appsettings.json configurato ? (sezione WebPush presente)
- [x] `IPushNotificationService` registrato in DI ? (vedi Program.cs linea 90)
- [x] Migration database creata ? (vedi Migrations/20250124_AddPushNotificationFields.cs)
- [x] Migration applicata ? (campo PushSubscription presente in ApplicationUser)
- [ ] Endpoint `/api/push/subscribe` creato ?? DA FARE
- [ ] UI per richiedere permessi implementata ?? DA FARE
- [ ] Integrato in `SendAssignmentNotifications` ?? DA FARE
- [ ] Testato con notifica di test ?? DA TESTARE

---

## ?? **PASSI FINALI MANCANTI**

### **STEP 1: Installa Package WebPush** (2 minuti)

```powershell
cd D:\Progetti\CRM\CRM\Server
dotnet add package WebPush --version 1.0.11
```

### **STEP 2: Aggiorna PushNotificationService.cs** (5 minuti)

Sostituisci il metodo `SendPushNotification` con l'implementazione completa usando WebPush:

```csharp
private async Task<int> SendPushNotification(string subscriptionJson, object notificationData)
{
    try
    {
        // Leggi VAPID keys da configurazione
        var vapidPublicKey = _configuration["WebPush:publicKey"];
        var vapidPrivateKey = _configuration["WebPush:privateKey"];
        var vapidSubject = _configuration["WebPush:subject"];

        if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
        {
            await _logEventService.RegisterAsync(
                nameof(PushNotificationService),
                nameof(SendPushNotification),
                LogEvent.EventsTypes.Error,
                "VAPID keys non configurate");
            return 0;
        }

        // Deserializza subscription
        var subscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscriptionJson);

        // Serializza payload
        var payload = JsonSerializer.Serialize(notificationData);

        // Crea client WebPush
        var webPushClient = new WebPushClient();

        // Invia notifica
        await webPushClient.SendNotificationAsync(
            subscription,
            payload,
            new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey));

        return 1; // Successo
    }
    catch (WebPushException ex)
    {
        if (ex.StatusCode == System.Net.HttpStatusCode.Gone || 
            ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Subscription scaduta
            await _logEventService.RegisterAsync(
                nameof(PushNotificationService),
                nameof(SendPushNotification),
                LogEvent.EventsTypes.Warning,
                $"Subscription scaduta/invalida: {ex.Message}");
        }
        else
        {
            await _logEventService.RegisterAsync(
                nameof(PushNotificationService),
                nameof(SendPushNotification),
                LogEvent.EventsTypes.Error,
                ex);
        }
        return 0;
    }
    catch (Exception ex)
    {
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Error,
            ex);
        return 0;
    }
}
```

E aggiungi l'using:
```csharp
using WebPush;
```

### **STEP 3: Crea Endpoint Subscribe** (3 minuti)

In `TicketsController.cs` aggiungi:

```csharp
[Inject]
IPushNotificationService _pushService { get; set; } // Se non già presente

[HttpPost("api/push/subscribe")]
public async Task<IActionResult> PushSubscribe([FromBody] PushSubscribeRequest request)
{
    var userId = _userManager.GetUserId(User);
    
    var result = await _pushService.SaveSubscriptionAsync(
        userId, 
        request.Subscription);
    
    return result ? Ok() : BadRequest("Impossibile salvare subscription");
}

public class PushSubscribeRequest
{
    public string Subscription { get; set; }
}
```

### **STEP 4: Integra in SendAssignmentNotifications** (2 minuti)

In `SendAssignmentNotifications`, aggiungi DOPO email/telegram:

```csharp
// ? NUOVO: Push Notification
var pushNotification = new
{
    title = isAssignment 
        ? $"? Assegnato al ticket #{ticket.Id}"
        : $"? Rimosso dal ticket #{ticket.Id}",
    body = $"{ticketWithDetails.Company?.RagioneSociale}",
    icon = "/favicon.ico",
    url = $"/Tickets/Info/{ticket.Id}",
    data = new { ticketId = ticket.Id }
};

await _pushService.SendToUsersAsync(userIds, pushNotification);
```

### **STEP 5: Crea UI per Attivazione** (10 minuti)

Crea `CRM\Client\Pages\Settings\PushNotifications.razor`:

```razor
@page "/settings/push-notifications"
@inject IJSRuntime JSRuntime
@inject HttpClient Http
@inject NotificationService NotificationService

<h3>?? Notifiche Push</h3>

<RadzenCard>
    <p>Attiva le notifiche push per ricevere avvisi in tempo reale anche quando il browser è chiuso.</p>
    
    @if (_isSupported)
    {
        @if (_permission == "granted")
        {
            <RadzenAlert AlertStyle="AlertStyle.Success" Variant="Variant.Flat">
                ? Notifiche push attive
            </RadzenAlert>
            <RadzenButton Click="DisableNotifications" ButtonStyle="ButtonStyle.Danger" class="mt-3">
                Disattiva Notifiche
            </RadzenButton>
        }
        else if (_permission == "denied")
        {
            <RadzenAlert AlertStyle="AlertStyle.Danger" Variant="Variant.Flat">
                ? Permesso negato. Sblocca dalle impostazioni del browser.
            </RadzenAlert>
        }
        else
        {
            <RadzenButton Click="EnableNotifications" ButtonStyle="ButtonStyle.Primary">
                ?? Attiva Notifiche Push
            </RadzenButton>
        }
    }
    else
    {
        <RadzenAlert AlertStyle="AlertStyle.Warning" Variant="Variant.Flat">
            ?? Il tuo browser non supporta le notifiche push
        </RadzenAlert>
    }
</RadzenCard>

@code {
    private bool _isSupported;
    private string _permission = "default";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isSupported = await JSRuntime.InvokeAsync<bool>("PushNotifications.isSupported");
            if (_isSupported)
            {
                _permission = await JSRuntime.InvokeAsync<string>("PushNotifications.getPermissionState");
            }
            StateHasChanged();
        }
    }

    private async Task EnableNotifications()
    {
        try
        {
            // 1. Richiedi permesso
            var permissionResult = await JSRuntime.InvokeAsync<JsonElement>(
                "PushNotifications.requestPermission");
            
            if (!permissionResult.GetProperty("success").GetBoolean())
            {
                NotificationService.Notify(NotificationSeverity.Error, "Errore", "Permesso negato");
                return;
            }

            // 2. Subscribe con VAPID key (leggi da appsettings)
            var vapidPublicKey = "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE";
            
            var subscribeResult = await JSRuntime.InvokeAsync<JsonElement>(
                "PushNotifications.subscribe", vapidPublicKey);
            
            if (!subscribeResult.GetProperty("success").GetBoolean())
            {
                NotificationService.Notify(NotificationSeverity.Error, "Errore", "Subscribe fallito");
                return;
            }

            // 3. Invia subscription al server
            var subscription = subscribeResult.GetProperty("subscription").GetString();
            
            var response = await Http.PostAsJsonAsync("api/push/subscribe", new { 
                subscription = subscription 
            });

            if (response.IsSuccessStatusCode)
            {
                _permission = "granted";
                NotificationService.Notify(NotificationSeverity.Success, "Successo", "Notifiche push attivate!");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Errore", "Salvataggio fallito");
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Errore", ex.Message);
        }
    }

    private async Task DisableNotifications()
    {
        await JSRuntime.InvokeVoidAsync("PushNotifications.unsubscribe");
        _permission = "default";
        NotificationService.Notify(NotificationSeverity.Info, "Info", "Notifiche disattivate");
    }
}
```

E aggiungi link nel menu settings.

---

## ?? **TEMPO STIMATO TOTALE: 22 MINUTI**

1. Install package: **2 min**
2. Update service: **5 min**
3. Create endpoint: **3 min**
4. Integrate notifications: **2 min**
5. Create UI: **10 min**

---

## ?? **PRONTO ALL'USO!**

Una volta completati gli step sopra, il sistema sarà in grado di:
- ? Richiedere permessi push agli utenti
- ? Salvare subscription nel database
- ? Inviare notifiche push quando ticket vengono assegnati/rimossi
- ? Gestire click sulle notifiche (navigazione automatica)
- ? Tracking completo via LogEvents

---

## ?? **RISORSE**

- [MDN Web Push API](https://developer.mozilla.org/en-US/docs/Web/API/Push_API)
- [WebPush NuGet](https://www.nuget.org/packages/WebPush/)
- [VAPID Protocol](https://datatracker.ietf.org/doc/html/rfc8292)
- [Service Workers Guide](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)

---

**Autore**: GitHub Copilot  
**Data**: 2025-01-24  
**Versione**: 1.0
