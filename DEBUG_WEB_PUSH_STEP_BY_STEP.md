# ?? DEBUG STEP-BY-STEP: Notifiche Push Non Appaiono

## ?? CHECKLIST DI DEBUG

Segui questi passi **IN ORDINE** e annota i risultati:

---

## ? STEP 1: Verifica Log Server

Esegui questa query SQL:

```sql
SELECT TOP 50
    Date,
    CASE EventType 
        WHEN 0 THEN '? Info'
        WHEN 1 THEN '?? Warning'
        WHEN 2 THEN '? Error'
    END AS [Type],
    SourceName,
    Message
FROM LogEvents
WHERE (SourceName LIKE '%Push%' 
   OR SourceName = 'TicketsController'
   OR Message LIKE '%push%'
   OR Message LIKE '%Push%'
   OR Message LIKE '%SendAssignment%')
  AND Date >= DATEADD(hour, -2, GETDATE())
ORDER BY Date DESC;
```

### **COSA CERCARE:**

#### **Scenario A: Log Completo (Server Funziona)**
```
[Timestamp] ? Info | PushNotificationService | ? Notifica push inviata con SUCCESSO!
[Timestamp] ? Info | PushNotificationService | ?? Invio notifica push via WebPushClient...
[Timestamp] ? Info | PushNotificationService | ? Payload preparato - Length: XXX bytes
[Timestamp] ? Info | PushNotificationService | ? Subscription deserializzata - Endpoint: https://fcm.googleapis.com/fcm/send/...
[Timestamp] ? Info | PushNotificationService | ? VAPID keys trovate - Subject: mailto:crm@easydms.net
[Timestamp] ? Info | PushNotificationService | Tentativo invio push notification. Subscription length: 365
```

**? Se vedi TUTTO questo:** Server invia correttamente. **Vai a STEP 2 (Problema Browser)**

---

#### **Scenario B: Log con Errori**
```
[Timestamp] ? Error | PushNotificationService | ? VAPID keys INVALIDE (HTTP 401)
```
**? Fix:** VAPID keys sbagliate. **Vai a FIX B1**

```
[Timestamp] ?? Warning | PushNotificationService | ?? Subscription scaduta/invalida (HTTP 410)
```
**? Fix:** Riattiva notifiche dal client. **Vai a FIX B2**

```
[Timestamp] ? Error | PushNotificationService | ? Errore imprevisto [ArgumentException]: You must pass in a subscription with at least a valid endpoint
```
**? Fix:** Subscription corrotta nel DB. **Vai a FIX B3**

---

#### **Scenario C: Nessun Log Push**
```
(Nessun risultato dalla query)
```

**? Il metodo `SendAssignmentNotifications` NON viene chiamato!** **Vai a STEP 3 (Debug Assegnazione)**

---

## ?? FIX B1: VAPID Keys Invalide

Le chiavi nel client e server devono essere IDENTICHE.

**Verifica in:**

### **Server: `appsettings.json`**
```json
"WebPush": {
  "subject": "mailto:crm@easydms.net",
  "publicKey": "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE",
  "privateKey": "QHGsivm28GHwWKhMkf6YRhBMG8QfJQjdS4BDIpDWiwM"
}
```

### **Client: `PushNotifications.razor`**
```csharp
var vapidPublicKey = "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE";
```

**? Se IDENTICHE:** Riavvia server e riprova

**? Se DIVERSE:** Allinea le chiavi e riavvia server

---

## ?? FIX B2: Subscription Scaduta

La subscription nel DB non è più valida.

**Fix:**

1. Vai su `/settings/push-notifications`
2. Click "Disattiva Notifiche"
3. Click "Attiva Notifiche Push"
4. Concedi permessi browser
5. Riassegna ticket e ricontrolla log

---

## ?? FIX B3: Subscription Corrotta (Endpoint N/A)

La subscription salvata non ha un endpoint valido.

**Fix:**

```sql
-- Elimina subscription corrotta
UPDATE AspNetUsers
SET PushSubscription = NULL, PushSubscriptionDate = NULL
WHERE Email = 'TUA_EMAIL@example.com';
```

Poi riattiva notifiche dal client (vedi FIX B2).

---

## ? STEP 2: Test Browser Locale

**SOLO se STEP 1 Scenario A (Server funziona)**

Apri Console Browser (F12 ? Console) ed esegui:

```javascript
// Test 1: Verifica permessi
console.log('Permessi:', Notification.permission);
// Deve mostrare: "granted"

// Test 2: Test notifica locale
PushNotifications.showTestNotification();
```

### **Risultato Test 2:**

#### **? Notifica APPARE**
- Browser funziona
- **Problema:** Server invia ma browser non riceve push dal servizio esterno
- **? Vai a STEP 2A (Debug Service Worker)**

#### **? Notifica NON appare**
- Permessi bloccati o browser in modalità silenziosa
- **? Vai a FIX C (Permessi Browser)**

---

## ?? STEP 2A: Debug Service Worker

Verifica che il Service Worker riceva eventi push:

```javascript
// Console Browser
navigator.serviceWorker.addEventListener('message', (event) => {
    console.log('[SW Message]:', event.data);
});

// Verifica SW attivo
navigator.serviceWorker.getRegistrations().then(regs => {
    console.log('Service Workers:', regs);
    regs.forEach(reg => {
        console.log('- Scope:', reg.scope);
        console.log('- Active:', reg.active);
        
        // Verifica subscription
        reg.pushManager.getSubscription().then(sub => {
            if (sub) {
                console.log('Subscription endpoint:', sub.endpoint);
            } else {
                console.log('? Nessuna subscription attiva!');
            }
        });
    });
});
```

### **Output Atteso:**
```
Service Workers: [ServiceWorkerRegistration]
- Scope: /
- Active: ServiceWorker { state: "activated", ... }
Subscription endpoint: https://fcm.googleapis.com/fcm/send/cZiEFAriidQ:APA91bFpv...
```

### **Se `Nessuna subscription attiva`:**
? Riattiva notifiche (vedi FIX B2)

---

## ?? FIX C: Permessi Browser Bloccati

### **Chrome/Edge:**

1. Vai su: `chrome://settings/content/notifications`
2. Nella ricerca, digita il tuo sito (es. `localhost`)
3. Verifica che sia in **"Consentiti"**
4. Se è in "Bloccati":
   - Click sull'icona del sito
   - Seleziona "Rimuovi"
   - Vai su `/settings/push-notifications`
   - Riattiva notifiche (chiederà nuovamente permesso)

### **Windows Focus Assist (Do Not Disturb):**

1. Impostazioni Windows ? Sistema ? Notifiche e azioni
2. Verifica "Assistente notifiche" sia **DISATTIVATO**

### **Test Finale Permessi:**

```javascript
// Console Browser
Notification.requestPermission().then(permission => {
    console.log('Permesso notifiche:', permission);
    if (permission === 'granted') {
        new Notification('Test', { body: 'Funziona!' });
    }
});
```

---

## ? STEP 3: Debug Assegnazione Ticket

**SOLO se STEP 1 Scenario C (Nessun log push)**

Il metodo `SendAssignmentNotifications` potrebbe non essere chiamato.

### **Verifica che l'assegnazione avvenga:**

```sql
-- Controlla log assegnazione
SELECT TOP 20 *
FROM LogEvents
WHERE SourceName = 'TicketsController'
  AND Message LIKE '%assign%'
  AND Date >= DATEADD(hour, -1, GETDATE())
ORDER BY Date DESC;
```

### **Dovresti vedere:**
```
? Ticket #1234: Assegnati 1 utenti
```

### **Se NON vedi NULLA:**
- L'endpoint `/api/Tickets/{id}/assign-users` non viene chiamato
- Oppure l'assegnazione fallisce prima di arrivare al push

**? Verifica che stai usando il dialog Assign.razor per assegnare gli utenti**

---

## ?? TEST MANUALE PUSH API

Se tutti gli step sopra falliscono, testa direttamente l'API push:

### **1. Ottieni il tuo User ID:**

```sql
SELECT Id, Email, NameComplete
FROM AspNetUsers
WHERE Email = 'TUA_EMAIL@example.com';
```

### **2. Chiama API Push direttamente (Postman/Swagger):**

```http
POST https://localhost:7001/api/Tickets/{id}/assign-users
Authorization: Bearer YOUR_JWT_TOKEN
Content-Type: application/json

{
  "ticketId": 1234,
  "userIds": ["USER_ID_QUI"]
}
```

### **3. Controlla log immediatamente:**

```sql
SELECT TOP 10 *
FROM LogEvents
WHERE Date >= DATEADD(minute, -5, GETDATE())
ORDER BY Date DESC;
```

---

## ?? MATRICE DECISION TREE

| Scenario STEP 1 | Scenario STEP 2 | Diagnosi | Fix |
|-----------------|-----------------|----------|-----|
| ? Log completo | ? Test locale funziona | Service Worker non riceve push | STEP 2A |
| ? Log completo | ? Test locale fallisce | Permessi browser | FIX C |
| ? Error 401 | - | VAPID keys sbagliate | FIX B1 |
| ?? Warning 410 | - | Subscription scaduta | FIX B2 |
| ? ArgumentException | - | Subscription corrotta | FIX B3 |
| ? Nessun log push | - | Assegnazione non chiama push | STEP 3 |

---

## ?? SE ANCORA NON FUNZIONA

Raccogli questi dati e inviameli:

### **1. Screenshot Log Server:**
```sql
SELECT TOP 30 Date, EventType, SourceName, Message
FROM LogEvents
WHERE Date >= DATEADD(hour, -1, GETDATE())
ORDER BY Date DESC;
```

### **2. Screenshot Console Browser:**
```javascript
// Esegui tutti questi comandi e screenshot risultato
console.log('1. Permessi:', Notification.permission);

navigator.serviceWorker.getRegistrations().then(regs => {
    console.log('2. SW Count:', regs.length);
    if (regs.length > 0) {
        regs[0].pushManager.getSubscription().then(sub => {
            console.log('3. Subscription:', sub);
            console.log('4. Endpoint:', sub ? sub.endpoint : 'N/A');
        });
    }
});

PushNotifications.showTestNotification();
console.log('5. Test notifica eseguito');
```

### **3. Verifica Subscription DB:**
```sql
SELECT Email, 
       PushSubscriptionDate,
       SUBSTRING(PushSubscription, 1, 200) AS SubscriptionPreview
FROM AspNetUsers
WHERE Email = 'TUA_EMAIL';
```

### **4. Descrivi:**
- Quale STEP ha fallito?
- Quale FIX hai provato?
- Quale errore hai visto?

---

**Segui la checklist IN ORDINE e segnala a quale STEP ti sei bloccato!** ??
