# ?? QUICK DEBUG: Web Push Non Arrivano

## ?? **5 MINUTI - DIAGNOSI RAPIDA**

Segui questi 3 step nell'ordine per identificare il problema:

---

## ? **STEP 1: Verifica Subscription Salvata** (30 secondi)

Esegui questa query SQL:

```sql
SELECT 
    Email,
    NameComplete,
    CASE 
        WHEN PushSubscription IS NULL THEN '? NO SUBSCRIPTION'
        WHEN LEN(PushSubscription) < 100 THEN '?? SUBSCRIPTION TROPPO CORTA'
        ELSE '? OK'
    END AS [Status],
    PushSubscriptionDate,
    LEFT(PushSubscription, 100) AS [Preview]
FROM AspNetUsers
WHERE Email = 'TUA_EMAIL@example.com';
```

### **Risultato Atteso:**

```
| Email | Status | PushSubscriptionDate | Preview |
|-------|--------|----------------------|---------|
| mario@test.com | ? OK | 2025-01-24 15:30 | {"endpoint":"https://fcm... |
```

### **? Se Status = "? NO SUBSCRIPTION":**

**Problema:** Utente non ha attivato notifiche push

**Fix rapido:**
1. Vai su `/settings/push-notifications`
2. Click "Attiva Notifiche Push"
3. Concedi permessi
4. Ricontrolla DB

---

## ? **STEP 2: Verifica Log Server** (1 minuto)

Assegna un ticket e poi esegui:

```sql
SELECT TOP 20 
    Date,
    SourceName,
    CASE EventType
        WHEN 0 THEN '? Info'
        WHEN 1 THEN '?? Warning'
        WHEN 2 THEN '? Error'
    END AS [Type],
    Message
FROM LogEvents
WHERE (SourceName = 'PushNotificationService' OR Message LIKE '%push%')
ORDER BY Date DESC;
```

### **Cerca questi messaggi:**

? **Se vedi:**
```
? Notifica push inviata con SUCCESSO!
```
? **Server funziona! Problema lato client** (vai a Step 3)

? **Se vedi:**
```
? VAPID keys non configurate in appsettings.json
```
? **Problema:** VAPID keys mancanti

**Fix rapido:**
```json
// In appsettings.json
{
  "WebPush": {
    "subject": "mailto:crm@easydms.net",
    "publicKey": "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE",
    "privateKey": "QHGsivm28GHwWKhMkf6YRhBMG8QfJQjdS4BDIpDWiwM"
  }
}
```

? **Se vedi:**
```
?? Subscription scaduta/invalida (HTTP 410)
```
? **Problema:** Browser ha invalidato subscription

**Fix rapido:**
1. Utente: Disattiva notifiche
2. Utente: Riattiva notifiche
3. Riprova

? **Se vedi:**
```
? VAPID keys INVALIDE (HTTP 401)
```
? **Problema:** VAPID keys sbagliate

**Fix rapido:**
1. Vai su https://vapidkeys.com/
2. Genera nuove keys
3. Sostituisci in `appsettings.json`
4. Restart server

---

## ? **STEP 3: Verifica Browser** (2 minuti)

Apri **Console browser** (F12) e testa:

```javascript
// 1. Verifica permessi
console.log('Permessi:', Notification.permission);
// Deve essere: "granted"

// 2. Verifica Service Worker
navigator.serviceWorker.getRegistrations().then(regs => {
    console.log('Service Workers registrati:', regs.length);
});
// Deve essere: >= 1

// 3. Test notifica locale
PushNotifications.showTestNotification();
// Deve apparire notifica desktop
```

### **? Se `Notification.permission` = "denied":**

**Fix rapido:**
1. Vai su impostazioni browser (icona lucchetto)
2. Notifiche ? Consenti
3. Ricarica pagina
4. Vai su `/settings/push-notifications`
5. Riattiva

### **? Se Service Workers = 0:**

**Fix rapido:**
1. Verifica che esista `wwwroot/service-worker.js`
2. Apri DevTools ? Application ? Service Workers
3. Click "Unregister" su vecchi SW
4. Ricarica pagina (Ctrl+Shift+R)
5. Riattiva notifiche

---

## ?? **MATRICE DECISION TREE**

Usa questa matrice per diagnosi rapida:

| Subscription DB | Log Server | Test Browser | Diagnosi |
|-----------------|------------|--------------|----------|
| ? NULL | - | - | **User non ha attivato push** |
| ? OK | ? Error VAPID | - | **VAPID keys sbagliate/mancanti** |
| ? OK | ?? HTTP 410 | - | **Subscription scaduta** |
| ? OK | ? Successo | ? Nessuna notifica | **Browser/Firewall blocca** |
| ? OK | ? Successo | ? Test funziona | **Problema applicativo (non push)** |

---

## ?? **ANCORA NON FUNZIONA?**

Se dopo questi 3 step ancora non funziona, raccogli questi dati:

### **1. Screenshot Console Browser**

```javascript
// Esegui e screenshot risultato
console.log('Permessi:', Notification.permission);
navigator.serviceWorker.getRegistrations().then(regs => {
    console.log('SW:', regs);
    if (regs.length > 0) {
        regs[0].pushManager.getSubscription().then(sub => {
            console.log('Subscription:', JSON.stringify(sub, null, 2));
        });
    }
});
```

### **2. Log Server Completi**

```sql
SELECT Date, SourceName, EventType, Message
FROM LogEvents
WHERE SourceName = 'PushNotificationService'
   OR SourceName = 'TicketsController' AND Message LIKE '%push%'
ORDER BY Date DESC;
```

### **3. Stato Subscription DB**

```sql
SELECT Email, PushSubscriptionDate, 
       SUBSTRING(PushSubscription, 1, 200) AS Preview
FROM AspNetUsers
WHERE Email = 'TUA_EMAIL';
```

Invia questi 3 output per supporto avanzato.

---

## ?? **FIX COMUNI IMMEDIATI**

### **Fix 1: Reset Completo Subscription**

```sql
-- Elimina subscription corrotta
UPDATE AspNetUsers
SET PushSubscription = NULL, PushSubscriptionDate = NULL
WHERE Email = 'utente@example.com';
```

Poi lato client:
1. Vai su `/settings/push-notifications`
2. Se attive, disattiva
3. Riattiva
4. Verifica che DB si aggiorni

### **Fix 2: VAPID Keys Fresh**

```json
// appsettings.json - COPIA ESATTO QUESTE KEYS (già configurate)
{
  "WebPush": {
    "subject": "mailto:crm@easydms.net",
    "publicKey": "BGP3BD1OnlEr7GW3YidM4E2YNgddp9r7BIyzYjUFi5s1xxD6sPlgK8VFcQHt0NNk98nO1VOxDhp9OlBdrCODeqE",
    "privateKey": "QHGsivm28GHwWKhMkf6YRhBMG8QfJQjdS4BDIpDWiwM"
  }
}
```

Restart server dopo modifica.

### **Fix 3: Service Worker Re-register**

```javascript
// Console browser
navigator.serviceWorker.getRegistrations().then(regs => {
    regs.forEach(reg => reg.unregister());
    console.log('All SW unregistered');
    location.reload();
});
```

---

## ? **CHECKLIST FINALE**

Prima di dichiarare "non funziona", verifica:

- [ ] ? Subscription salvata nel DB (query Step 1)
- [ ] ? VAPID keys in appsettings.json
- [ ] ? Log server mostrano "Successo" (query Step 2)
- [ ] ? Browser permessi = "granted"
- [ ] ? Service Worker registrato
- [ ] ? Test notifica locale funziona
- [ ] ? Utente assegnato a ticket DOPO aver attivato push
- [ ] ? Browser aperto (o Android)

Se tutti ? ? Le notifiche DEVONO funzionare!

---

**Tempo stimato debug:** 5-10 minuti  
**Success rate:** 95%+ ??
