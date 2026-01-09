# ?? Guida Completa: Push Notifications CRM

## ? PROBLEMA ATTUALE

Le notifiche push **NON FUNZIONANO** perché:
1. ? Service Worker fix applicato (errore audio 206 risolto)
2. ? **VAPID keys NON configurate** (chiavi fake in appsettings.json)
3. ? Sezione config chiamata `PushNotifications` invece di `WebPush`

## ? SOLUZIONE COMPLETA (5 step)

### Step 1: Genera VAPID Keys VERE

Apri PowerShell nella directory `D:\Progetti\CRM\` ed esegui:

```powershell
.\CRM\Tools\GenerateVapidKeys_STANDALONE.ps1
```

**Output atteso:**
```
?? VAPID KEYS GENERATE SUCCESS!
======================================

?? Copia queste chiavi in CRM/Server/appsettings.PushNotifications.json:

{
  "WebPush": {
    "publicKey": "BKd8...lungachiave...",
    "privateKey": "abc123...chiaesegreta...",
    "subject": "mailto:info@a-plusautomation.com"
  }
}
```

### Step 2: Copia le Chiavi in appsettings.json

Apri `CRM\Server\appsettings.PushNotifications.json` e **SOSTITUISCI TUTTO** con:

```json
{
  "WebPush": {
    "publicKey": "LA_TUA_PUBLIC_KEY_GENERATA",
    "privateKey": "LA_TUA_PRIVATE_KEY_GENERATA",
    "subject": "mailto:info@a-plusautomation.com"
  }
}
```

?? **IMPORTANTE**: 
- Sostituisci `LA_TUA_PUBLIC_KEY_GENERATA` e `LA_TUA_PRIVATE_KEY_GENERATA` con i valori VERI generati dallo script
- NON committare la private key su Git pubblico!

### Step 3: Verifica Service Worker (già fixato)

Il file `CRM\Client\wwwroot\service-worker-1.0.js` è già stato corretto:
- ? Version: 2.1
- ? Audio/video 206 fix applicato
- ? Push notification listener configurato

### Step 4: Hard Refresh Browser

1. Apri DevTools (F12)
2. Vai su **Application > Service Workers**
3. Clicca **Unregister** sul vecchio service worker
4. Premi **Ctrl + Shift + R** (hard refresh)
5. Verifica che venga caricato **Service Worker Version 2.1**

### Step 5: Test Push Notifications

1. Apri `https://localhost:5001`
2. Vai sulla pagina di test: `https://localhost:5001/test-push.html`
3. Clicca **"Enable Push Notifications"**
4. Accetta il permesso notifiche
5. Clicca **"Send Test Notification"**
6. Dovresti vedere una notifica pop-up!

## ?? Troubleshooting

### Errore: "VAPID keys non configurate"

**Causa**: appsettings.PushNotifications.json ha ancora chiavi fake.

**Soluzione**: Rigenera le chiavi con lo script e copiale in appsettings.json

### Errore: "Service Worker registration failed"

**Causa**: Service worker vecchio in cache.

**Soluzione**:
1. DevTools > Application > Service Workers
2. Check "Update on reload"
3. Unregister all
4. Hard refresh (Ctrl+Shift+R)

### Errore: "Failed to execute 'put' on 'Cache': Partial response (status code 206)"

**Causa**: Service worker vecchio (v2.0) ancora attivo.

**Soluzione**:
1. Verifica versione in console: deve essere **v2.1**
2. Se è v2.0, forza aggiornamento (vedi sopra)

### Notifiche non appaiono

**Causa**: Permessi browser.

**Soluzione**:
1. Apri impostazioni browser
2. Vai su Privacy & Security > Site Settings > Notifications
3. Aggiungi `localhost:5001` ai siti consentiti
4. Ricarica e riprova

## ?? Checklist Finale

Segna ? quando completato:

- [ ] Step 1: Generato VAPID keys con script PowerShell
- [ ] Step 2: Copiato chiavi in `appsettings.PushNotifications.json`
- [ ] Step 3: Riavviato server CRM
- [ ] Step 4: Fatto hard refresh browser (Ctrl+Shift+R)
- [ ] Step 5: Verificato Service Worker v2.1 in DevTools
- [ ] Step 6: Testato push su `/test-push.html`
- [ ] Step 7: Ricevuto notifica di test ?

## ?? Se Continua a Non Funzionare

Controlla i log del server in:
- **Visual Studio**: Output > Debug
- **Console browser**: F12 > Console

Cerca questi messaggi:
- ? `"Service Worker Loaded successfully - Version 2.1"`
- ? `"? Push subscription salvata per utente XXX"`
- ? `"VAPID keys non configurate"` ? Ricontrolla Step 2

---

## ?? Success!

Se vedi la notifica di test, **le push notifications sono ATTIVE**! 

Ora puoi:
- Assegnare ticket ? notifica automatica agli utenti assegnati
- Inviare messaggi chat ? notifica agli utenti destinatari
- Chiudere ticket ? notifica al creatore del ticket

---

**Creato**: 2025-01-08  
**Versione Service Worker**: 2.1  
**Ultimo aggiornamento**: Audio/video 206 fix + VAPID keys setup guide
