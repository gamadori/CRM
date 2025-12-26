# ?? FIX: Endpoint N/A - Subscription Malformata

## ?? **PROBLEMA IDENTIFICATO**

Dal log:
```
? Error: You must pass in a subscription with at least a valid endpoint
? Subscription deserializzata - Endpoint: N/A...
```

**Causa:** La subscription salvata nel database **NON** contiene un endpoint valido.

---

## ?? **SOLUZIONE COMPLETA**

### **STEP 1: Pulisci Subscription Corrotta nel DB**

Esegui questa query SQL:

```sql
-- Verifica quale utente ha subscription corrotta
SELECT 
    Id,
    Email,
    NameComplete,
    LEFT(PushSubscription, 200) AS SubscriptionPreview,
    PushSubscriptionDate
FROM AspNetUsers
WHERE PushSubscription IS NOT NULL
ORDER BY PushSubscriptionDate DESC;
```

**Cerca subscription che inizia così:**
```json
? SBAGLIATO: {"endpoint":null,...}  o  {"endpoint":"N/A",...}
? CORRETTO: {"endpoint":"https://fcm.googleapis.com/fcm/send/...
```

**Se trovi subscription corrotte, eliminale:**

```sql
-- OPZIONE A: Elimina solo per un utente specifico
UPDATE AspNetUsers
SET PushSubscription = NULL, PushSubscriptionDate = NULL
WHERE Email = 'utente@example.com';

-- OPZIONE B: Elimina TUTTE le subscription corrotte (usa con cautela!)
UPDATE AspNetUsers
SET PushSubscription = NULL, PushSubscriptionDate = NULL
WHERE PushSubscription IS NOT NULL 
  AND (PushSubscription NOT LIKE '%https://fcm.googleapis.com%'
   AND PushSubscription NOT LIKE '%https://updates.push.services.mozilla.com%'
   AND PushSubscription NOT LIKE '%https://web.push.apple.com%');
```

---

### **STEP 2: Fix Client-Side Endpoint**

Ho già fixato i file:
- ? `PushNotifications.razor` ? Endpoint corretto: `api/Tickets/api/push/subscribe`
- ? `PushNotificationService.cs` ? Logging aggiunto per debug

---

### **STEP 3: Riavvia Server**

Riavvia il server per:
1. ? Ricaricare `appsettings.json` (fix VAPID subject)
2. ? Applicare nuovo logging

---

### **STEP 4: Riattiva Notifiche (Client)**

**Utente deve:**

1. Vai su `/settings/push-notifications`

2. **Se notifiche GIÀ attive:**
   - Click "Disattiva Notifiche"
   - Attendi conferma
   - Click "Attiva Notifiche Push"
   - Concedi permessi quando browser chiede
   - Attendi messaggio "Notifiche push attivate con successo!"

3. **Se notifiche NON attive:**
   - Click "Attiva Notifiche Push"
   - Concedi permessi quando browser chiede
   - Attendi messaggio "Notifiche push attivate con successo!"

---

### **STEP 5: Verifica Subscription Salvata**

Dopo aver attivato le notifiche, controlla il log server:

```sql
SELECT TOP 10 Date, Message
FROM LogEvents
WHERE SourceName = 'PushNotificationService'
  AND Message LIKE '%DEBUG%'
ORDER BY Date DESC;
```

**Dovresti vedere:**
```
? DEBUG: Ricevuta subscription per user abc123. 
   JSON length: 365. 
   Preview: {"endpoint":"https://fcm.googleapis.com/fcm/send/cXyz...","expirationTime":null,"keys":{"p256dh":"BMfQ...","auth":"dGVz..."}}...
```

**Verifica che `endpoint` inizi con:**
- ? `https://fcm.googleapis.com/fcm/send/...` (Chrome/Edge)
- ? `https://updates.push.services.mozilla.com/...` (Firefox)
- ? `https://web.push.apple.com/...` (Safari)

---

### **STEP 6: Test Invio Notifica**

1. Assegna un ticket all'utente

2. Controlla log:

```sql
SELECT TOP 20 Date, 
       CASE EventType WHEN 0 THEN '?' WHEN 1 THEN '??' WHEN 2 THEN '?' END AS [Tipo],
       Message
FROM LogEvents
WHERE (SourceName = 'PushNotificationService' OR Message LIKE '%push%')
  AND Date >= DATEADD(minute, -10, GETDATE())
ORDER BY Date DESC;
```

**Log Atteso (SUCCESSO):**
```
? Tentativo invio push notification. Subscription length: 365
? VAPID keys trovate - Subject: mailto:crm@easydms.net, ...
? Subscription deserializzata - Endpoint: https://fcm.googleapis.com/fcm/send/cXyz...
? Payload preparato - Length: 170 bytes
?? Invio notifica push via WebPushClient...
? Notifica push inviata con SUCCESSO!
```

**Se vedi ancora `Endpoint: N/A`:**
- ? La subscription non è stata ricreata correttamente
- ? Torna allo STEP 4 e riprova

---

## ?? **DIAGNOSI ALTERNATIVA**

Se il problema persiste, esegui questi test dal browser:

### **Test JavaScript (Console Browser F12):**

```javascript
// 1. Verifica Service Worker
navigator.serviceWorker.getRegistrations().then(regs => {
    console.log('Service Workers:', regs);
    regs.forEach(reg => {
        console.log('SW scope:', reg.scope);
        console.log('SW active:', reg.active);
    });
});

// 2. Ottieni subscription attuale
navigator.serviceWorker.ready.then(reg => {
    reg.pushManager.getSubscription().then(sub => {
        if (sub) {
            console.log('Subscription:', JSON.stringify(sub, null, 2));
            console.log('Endpoint:', sub.endpoint);
        } else {
            console.log('? Nessuna subscription attiva');
        }
    });
});

// 3. Verifica permessi
console.log('Notification.permission:', Notification.permission);
```

**Output Atteso:**
```javascript
Service Workers: [ServiceWorkerRegistration]
SW scope: "/"
SW active: ServiceWorker
Subscription: {
  "endpoint": "https://fcm.googleapis.com/fcm/send/cXyzAbc123...",
  "expirationTime": null,
  "keys": {
    "p256dh": "BMfQ...",
    "auth": "dGVz..."
  }
}
Endpoint: https://fcm.googleapis.com/fcm/send/cXyzAbc123...
Notification.permission: "granted"
```

**Se `endpoint` è `null` o `undefined`:**
- ? Service Worker non ha creato subscription valida
- ? Prova a disinstallare Service Worker:

```javascript
// Disinstalla tutti i Service Workers
navigator.serviceWorker.getRegistrations().then(regs => {
    regs.forEach(reg => reg.unregister());
    console.log('Tutti i SW disinstallati');
    location.reload();
});
```

Poi riattiva notifiche.

---

## ?? **ERRORI COMUNI E FIX**

### **Errore 1: Endpoint ancora N/A dopo riattivazione**

**Causa:** JavaScript non serializza correttamente la subscription

**Fix:**

1. Apri DevTools ? Application ? Service Workers
2. Click "Unregister" su tutti i SW
3. Ricarica pagina (Ctrl+Shift+R)
4. Riattiva notifiche

---

### **Errore 2: Subscribe fallisce senza errori**

**Causa:** VAPID public key sbagliata nel client

**Verifica in `PushNotifications.razor`:**
```csharp
var vapidPublicKey = "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE";
```

**Deve corrispondere ESATTAMENTE a `appsettings.json`:**
```json
"publicKey": "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE"
```

---

### **Errore 3: 401 Unauthorized quando salva subscription**

**Causa:** User non autenticato

**Fix:**
- Logout e re-login
- Riattiva notifiche

---

## ? **CHECKLIST FINALE**

Prima di dichiarare "funziona":

- [ ] ? DB: Subscription eliminata (query STEP 1)
- [ ] ? Server: Riavviato
- [ ] ? Client: Notifiche riattivate
- [ ] ? Log: Vedi `"DEBUG: Ricevuta subscription"` con endpoint valido
- [ ] ? Log: Vedi `"Subscription deserializzata - Endpoint: https://fcm..."`
- [ ] ? Test: Assegnato ticket
- [ ] ? Log: Vedi `"Notifica push inviata con SUCCESSO!"`
- [ ] ? Browser: Notifica appare

Se tutti ? ? **PROBLEMA RISOLTO!** ??

---

## ?? **QUERY DIAGNOSTICA COMPLETA**

Dopo aver seguito tutti gli step, esegui questa query per vedere lo stato finale:

```sql
-- Dashboard finale Web Push
SELECT 
    u.Email,
    u.NameComplete,
    u.PushSubscriptionDate AS [Registered],
    CASE 
        WHEN u.PushSubscription IS NULL THEN '? NO SUBSCRIPTION'
        WHEN u.PushSubscription LIKE '%https://fcm.googleapis.com%' THEN '? Chrome/Edge'
        WHEN u.PushSubscription LIKE '%https://updates.push.services.mozilla.com%' THEN '? Firefox'
        WHEN u.PushSubscription LIKE '%https://web.push.apple.com%' THEN '? Safari'
        ELSE '?? ENDPOINT SCONOSCIUTO'
    END AS [Status],
    LEFT(u.PushSubscription, 150) AS [Subscription Preview]
FROM AspNetUsers u
WHERE u.Email LIKE '%@%'
ORDER BY u.PushSubscriptionDate DESC;
```

---

**Segui gli step in ordine e segnala in quale step hai problemi!** ??

---

**Data Fix:** 25 Gennaio 2025  
**Problema:** Endpoint N/A in subscription  
**Causa:** Subscription malformata salvata nel DB  
**Fix:** Pulizia DB + riattivazione client-side  
