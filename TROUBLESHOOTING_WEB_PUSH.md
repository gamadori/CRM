# ?? TROUBLESHOOTING: Web Push Non Arrivano

## ?? CHECKLIST DIAGNOSTICA COMPLETA

Segui questi passi **IN ORDINE** per identificare il problema:

---

## ? **STEP 1: Verifica Permessi Browser**

### **Test Client-Side:**

1. Apri la **Console del browser** (F12 ? Console)
2. Esegui questi comandi:

```javascript
// 1. Verifica supporto
PushNotifications.isSupported()
// Deve ritornare: true

// 2. Verifica permessi
PushNotifications.getPermissionState()
// Deve ritornare: "granted" (NON "denied" o "default")

// 3. Verifica Service Worker
navigator.serviceWorker.getRegistrations().then(regs => {
    console.log('Service Workers:', regs);
    regs.forEach(reg => console.log('SW scope:', reg.scope));
});
// Deve mostrare almeno 1 service worker registrato
```

### **? SE FALLISCE:**

**Problema:** Permessi non concessi o browser non supportato

**Soluzione:**
1. Vai su `/settings/push-notifications`
2. Click "Attiva Notifiche Push"
3. Concedi permessi quando il browser chiede
4. Ricarica pagina

---

## ? **STEP 2: Verifica Subscription Salvata**

### **Test Database:**

Esegui questa query SQL:

```sql
-- Verifica se la subscription è salvata
SELECT 
    Id,
    Email,
    NameComplete,
    PushSubscription,
    PushSubscriptionDate
FROM AspNetUsers
WHERE Email = 'TUA_EMAIL@example.com';
```

### **Risultato Atteso:**

```
| Id | Email | PushSubscription | PushSubscriptionDate |
|----|-------|------------------|----------------------|
| abc123 | mario@test.com | {"endpoint":"https://fcm.googleapis.com/fcm/send/...", ...} | 2025-01-24 15:30:00 |
```

### **? SE `PushSubscription` È NULL:**

**Problema:** La subscription non è stata salvata sul server

**Soluzione:**

1. Apri **Network tab** del browser (F12 ? Network)
2. Vai su `/settings/push-notifications`
3. Click "Attiva Notifiche Push"
4. Cerca chiamata `POST /api/push/subscribe`
5. Verifica che ritorni **200 OK**

**Se vedi errore 401/403:**
- User non autenticato ? Rifare login

**Se vedi errore 500:**
- Guarda i log del server in `LogEvents`

---

## ? **STEP 3: Verifica VAPID Keys**

### **Test appsettings.json:**

Apri `CRM\Server\appsettings.json` e verifica:

```json
{
  "WebPush": {
    "subject": "mailto:crm@easydms.net",
    "publicKey": "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE",
    "privateKey": "QHGsivm28GHwWKhMkf6YRhBMG8QfJQjdS4BDIpDWiwM"
  }
}
```

### **? SE MANCANO O SONO VUOTE:**

**Problema:** VAPID keys non configurate

**Soluzione:**
1. Vai su https://vapidkeys.com/
2. Genera nuove keys
3. Copia/incolla in `appsettings.json`
4. Riavvia server

---

## ? **STEP 4: Test Notifica Manuale**

### **Test JavaScript (Console Browser):**

```javascript
// 1. Mostra notifica locale (NON via server)
PushNotifications.showTestNotification()

// Deve apparire notifica desktop/mobile ?
```

### **? SE NON APPARE:**

**Problema:** Permessi o Service Worker non funzionante

**Soluzione:**
1. Vai su `chrome://settings/content/notifications` (Chrome)
2. Verifica che il sito sia in "Consentiti"
3. Se bloccato, rimuovi e aggiungi nuovamente

---

## ? **STEP 5: Test Invio Server-Side**

### **Test Endpoint API:**

Usa **Postman** o **Swagger** per testare:

```http
POST https://localhost:7001/api/push/subscribe
Authorization: Bearer YOUR_JWT_TOKEN
Content-Type: application/json

{
  "subscription": "{\"endpoint\":\"https://fcm.googleapis.com/fcm/send/...\",\"keys\":{\"p256dh\":\"...\",\"auth\":\"...\"}}"
}
```

**Risposta Attesa:**
```json
{
  "message": "Subscription salvata con successo"
}
```

### **? SE FALLISCE:**

Controlla i log in `LogEvents`:

```sql
SELECT TOP 10 *
FROM LogEvents
WHERE SourceName = 'PushNotificationService'
ORDER BY Date DESC;
```

**Errori comuni:**
- `VAPID keys non configurate` ? Vedi Step 3
- `Impossibile deserializzare subscription` ? Subscription JSON malformata

---

## ? **STEP 6: Test Invio Notifica Reale**

### **Test Assegnazione Ticket:**

1. **Utente A:** Attiva notifiche push (`/settings/push-notifications`)
2. **Utente B (Manager):** Assegna ticket a Utente A
3. **Verifica:** Utente A dovrebbe ricevere notifica

### **Debug Logging:**

Aggiungi logging temporaneo in `SendAssignmentNotifications`:

```csharp
// In TicketsController.cs
private async Task SendAssignmentNotifications(...)
{
    // ...codice esistente...
    
    // ? DEBUG: Log prima di inviare push
    await _logEventService.RegisterAsync(
        nameof(TicketsController), 
        nameof(SendAssignmentNotifications), 
        LogEvent.EventsTypes.Info, 
        $"DEBUG: Invio push a {userIds.Count} utenti: {string.Join(", ", userIds)}");
    
    var pushSent = await _pushService.SendToUsersAsync(userIds, pushNotification);
    
    // ? DEBUG: Log dopo invio
    await _logEventService.RegisterAsync(
        nameof(TicketsController), 
        nameof(SendAssignmentNotifications), 
        LogEvent.EventsTypes.Info, 
        $"DEBUG: Push inviate con successo: {pushSent}/{userIds.Count}");
}
```

Poi controlla `LogEvents`:

```sql
SELECT *
FROM LogEvents
WHERE Message LIKE '%DEBUG:%'
ORDER BY Date DESC;
```

---

## ? **STEP 7: Verifica Subscription JSON Format**

### **Test Formato Subscription:**

Il JSON salvato nel DB deve avere questa struttura:

```json
{
  "endpoint": "https://fcm.googleapis.com/fcm/send/...",
  "expirationTime": null,
  "keys": {
    "p256dh": "BASE64_STRING_HERE",
    "auth": "BASE64_STRING_HERE"
  }
}
```

### **? SE È MALFORMATO:**

**Problema:** JavaScript non serializza correttamente

**Verifica in Console:**

```javascript
// Ottieni subscription corrente
navigator.serviceWorker.ready.then(reg => {
    reg.pushManager.getSubscription().then(sub => {
        console.log('Subscription:', JSON.stringify(sub));
    });
});
```

---

## ? **STEP 8: Verifica Network Connectivity**

### **Test Connessione Push Server:**

Le notifiche passano attraverso:
- **Chrome/Edge:** Firebase Cloud Messaging (FCM)
- **Firefox:** Mozilla Push Service
- **Safari:** Apple Push Notification Service (APNS)

### **Test:**

```javascript
// Verifica endpoint subscription
navigator.serviceWorker.ready.then(reg => {
    reg.pushManager.getSubscription().then(sub => {
        console.log('Endpoint:', sub.endpoint);
        // Chrome: https://fcm.googleapis.com/...
        // Firefox: https://updates.push.services.mozilla.com/...
    });
});
```

### **? SE ENDPOINT È BLOCCATO:**

**Problema:** Firewall/proxy blocca connessioni push

**Soluzione:**
- Verifica firewall aziendale
- Prova da rete diversa (hotspot mobile)
- Disabilita VPN temporaneamente

---

## ? **STEP 9: Test con Logging Avanzato**

Aggiungi logging dettagliato in `PushNotificationService.cs`:

```csharp
private async Task<int> SendPushNotification(string subscriptionJson, object notificationData)
{
    try
    {
        // ? LOG 1: Inizio invio
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Info,
            $"DEBUG: Invio push. Subscription: {subscriptionJson.Substring(0, Math.Min(50, subscriptionJson.Length))}...");
        
        var vapidPublicKey = _configuration["WebPush:publicKey"];
        var vapidPrivateKey = _configuration["WebPush:privateKey"];
        var vapidSubject = _configuration["WebPush:subject"];

        // ? LOG 2: VAPID keys
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Info,
            $"DEBUG: VAPID - Public: {vapidPublicKey?.Substring(0, 20)}..., Subject: {vapidSubject}");

        if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
        {
            await _logEventService.RegisterAsync(
                nameof(PushNotificationService),
                nameof(SendPushNotification),
                LogEvent.EventsTypes.Error,
                "VAPID keys non configurate in appsettings.json");
            return 0;
        }

        var subscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscriptionJson);

        if (subscription == null)
        {
            await _logEventService.RegisterAsync(
                nameof(PushNotificationService),
                nameof(SendPushNotification),
                LogEvent.EventsTypes.Error,
                "Impossibile deserializzare subscription");
            return 0;
        }

        // ? LOG 3: Subscription deserializzata
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Info,
            $"DEBUG: Subscription deserializzata. Endpoint: {subscription.Endpoint?.Substring(0, 50)}...");

        var payload = JsonSerializer.Serialize(notificationData);

        // ? LOG 4: Payload
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Info,
            $"DEBUG: Payload: {payload}");

        var webPushClient = new WebPushClient();

        // ? LOG 5: Prima di inviare
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Info,
            "DEBUG: Invio notifica via WebPushClient...");

        await webPushClient.SendNotificationAsync(
            subscription,
            payload,
            new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey));

        // ? LOG 6: Successo
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Info,
            "DEBUG: Notifica inviata con SUCCESSO! ?");

        return 1;
    }
    catch (WebPushException ex)
    {
        // ? LOG 7: Errore WebPush
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Error,
            $"DEBUG: WebPushException - StatusCode: {ex.StatusCode}, Message: {ex.Message}, StackTrace: {ex.StackTrace}");
        
        if (ex.StatusCode == System.Net.HttpStatusCode.Gone || 
            ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await _logEventService.RegisterAsync(
                nameof(PushNotificationService),
                nameof(SendPushNotification),
                LogEvent.EventsTypes.Warning,
                $"Subscription scaduta/invalida: {ex.Message}");
        }
        return 0;
    }
    catch (Exception ex)
    {
        // ? LOG 8: Errore generico
        await _logEventService.RegisterAsync(
            nameof(PushNotificationService),
            nameof(SendPushNotification),
            LogEvent.EventsTypes.Error,
            $"DEBUG: Exception generale - Type: {ex.GetType().Name}, Message: {ex.Message}, StackTrace: {ex.StackTrace}");
        return 0;
    }
}
```

Poi assegna un ticket e controlla `LogEvents` per vedere ESATTAMENTE dove si ferma.

---

## ?? **ERRORI COMUNI E SOLUZIONI**

### **1. "VAPID keys non configurate"**

```
? Errore: VAPID keys non configurate in appsettings.json
```

**Causa:** `appsettings.json` manca sezione `WebPush` o è vuota

**Soluzione:**
```json
{
  "WebPush": {
    "subject": "mailto:tua-email@example.com",
    "publicKey": "TUA_PUBLIC_KEY",
    "privateKey": "TUA_PRIVATE_KEY"
  }
}
```

---

### **2. "Subscription scaduta/invalida"**

```
? Errore: WebPushException - StatusCode: 410 Gone
```

**Causa:** Browser ha invalidato la subscription (es. dopo clear cache)

**Soluzione:**
1. Utente deve disattivare notifiche
2. Riattivare notifiche
3. Nuova subscription verrà creata

---

### **3. "Impossibile deserializzare subscription"**

```
? Errore: Impossibile deserializzare subscription
```

**Causa:** JSON malformato nel DB

**Soluzione:**
```sql
-- Elimina subscription corrotta
UPDATE AspNetUsers
SET PushSubscription = NULL, PushSubscriptionDate = NULL
WHERE Email = 'utente@example.com';
```

Poi riattiva notifiche dal client.

---

### **4. Notifiche arrivano solo con browser aperto**

```
? Browser aperto ? Notifiche arrivano
? Browser chiuso ? Notifiche NON arrivano
```

**Causa:** Normale su desktop (vedi documentazione)

**Soluzione:**
- Su **Android**: Funziona anche chiuso ?
- Su **Desktop**: Mantieni tab aperta (best practice)
- Su **iOS**: Installa come PWA

---

### **5. Service Worker non registrato**

```
? Errore: navigator.serviceWorker.getRegistrations() ? []
```

**Causa:** Service Worker non si registra

**Soluzione:**

Verifica che `index.html` abbia:

```html
<script>navigator.serviceWorker.register('service-worker.js');</script>
```

E che `service-worker.js` esista in `wwwroot/`.

---

## ?? **QUERY DIAGNOSTICA COMPLETA**

Esegui questa query per vedere lo stato completo:

```sql
-- Dashboard diagnostica Web Push
SELECT 
    u.Email,
    u.NameComplete,
    CASE 
        WHEN u.PushSubscription IS NULL THEN '? NO'
        ELSE '? SÌ'
    END AS [Has Subscription],
    u.PushSubscriptionDate AS [Registered On],
    DATEDIFF(day, u.PushSubscriptionDate, GETDATE()) AS [Days Ago],
    LEFT(u.PushSubscription, 50) AS [Subscription Preview]
FROM AspNetUsers u
WHERE u.Email LIKE '%@%' -- Solo utenti reali
ORDER BY u.PushSubscriptionDate DESC;
```

---

## ? **TEST FINALE DI VERIFICA**

Una volta risolto, testa end-to-end:

1. ? **Utente A:** Attiva push (`/settings/push-notifications`)
2. ? **Verifica DB:** `SELECT PushSubscription FROM AspNetUsers WHERE Email = 'userA@test.com'` ? NOT NULL
3. ? **Utente B:** Assegna ticket a Utente A
4. ? **Verifica Log:** `SELECT * FROM LogEvents WHERE Message LIKE '%Push%' ORDER BY Date DESC`
5. ? **Risultato:** Notifica appare su dispositivo di Utente A

---

## ?? **SUPPORTO**

Se ancora non funziona dopo tutti questi step, inviami:

1. **Screenshot** della console browser (F12)
2. **Query LogEvents** con errori:
   ```sql
   SELECT TOP 20 *
   FROM LogEvents
   WHERE SourceName LIKE '%Push%' 
      OR Message LIKE '%Push%'
   ORDER BY Date DESC;
   ```
3. **Risultato query subscription**:
   ```sql
   SELECT Email, PushSubscription, PushSubscriptionDate
   FROM AspNetUsers
   WHERE Email = 'TUA_EMAIL';
   ```

---

**Buon debugging!** ????
