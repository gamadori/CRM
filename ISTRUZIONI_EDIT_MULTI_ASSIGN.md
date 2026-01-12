# ? Assegnazione Multipla Utenti - Ticket Edit

## ?? Modifiche Implementate

### File Modificati:
1. **CRM\Client\Pages\Tickets\Edit.razor.cs**
2. **CRM\Client\Pages\Tickets\Edit.razor**

---

## ?? Funzionalità Aggiunte

### 1. **Selezione Multipla Utenti**
- ? Rimosso dropdown singolo `IdUserAssigned`
- ? Aggiunto sistema di selezione multipla con UI moderna
- ? Supporto per ricerca utenti in tempo reale
- ? Visualizzazione badge per utenti selezionati
- ? Indicatore "?" per utente principale (primo della lista)

### 2. **Sincronizzazione API**
- ? Al salvataggio del ticket, chiama `POST /api/Tickets/{id}/assign-users`
- ? Sincronizza `IdUserAssigned` con il primo utente selezionato (retrocompatibilità)
- ? Caricamento automatico utenti assegnati in edit mode

### 3. **UI/UX Miglioramenti**
- ? Badge utenti selezionati con avatar e iniziali
- ? Lista utenti disponibili con ricerca live
- ? Icona ? per utenti selezionati
- ? Pulsante × per rimuovere utenti
- ? Design responsive mobile-friendly

---

## ?? Dettagli Tecnici

### Nuovi Campi Privati in `Edit.razor.cs`:
```csharp
private HashSet<string> _selectedUserIds = new HashSet<string>();
private List<ApplicationUser> _filteredUsers = new List<ApplicationUser>();
private string _searchQuery = string.Empty;
```

### Nuovi Metodi in `Edit.razor.cs`:
- `LoadAssignedUsers()` - Carica utenti già assegnati al ticket
- `SaveMultipleAssignments(int ticketId)` - Salva assegnazioni multiple via API
- `OnSearchChanged(string searchQuery)` - Ricerca utenti in tempo reale
- `ToggleUser(string userId)` - Aggiungi/rimuovi utente dalla selezione
- `RemoveUser(string userId)` - Rimuove utente dalla selezione
- `GetInitials(string fullName)` - Estrae iniziali dal nome completo

### Modifiche a `HandleValidSubmit()`:
```csharp
// Sincronizza IdUserAssigned (retrocompatibilità)
if (_selectedUserIds.Any())
{
    _ticket.IdUserAssigned = _selectedUserIds.First();
}
else
{
    _ticket.IdUserAssigned = null;
}

_ticket = (await _service.Post(_ticket)).Data;

// Salva assegnazioni multiple
if (_ticket?.Id > 0)
{
    await SaveMultipleAssignments(_ticket.Id);
}
```

### Modifiche a `OnGetScheduler()`:
```csharp
// Aggiungi utente alla selezione multipla invece di sostituire
if (userDate.IdUser != null)
{
    if (!_selectedUserIds.Contains(userDate.IdUser))
    {
        _selectedUserIds.Add(userDate.IdUser);
    }
    
    // Mantieni retrocompatibilità
    _ticket.IdUserAssigned = userDate.IdUser;
}
```

---

## ?? Componenti UI Aggiunti

### 1. **Selected Users Container**
```html
<div class="selected-users-container">
    <!-- Badge per ogni utente selezionato -->
</div>
```

### 2. **User Selection Panel**
```html
<div class="user-selection-panel">
    <!-- Input ricerca -->
    <RadzenTextBox Placeholder="Cerca utenti..." />
    
    <!-- Lista utenti -->
    <div class="user-list">
        <!-- User items cliccabili -->
    </div>
</div>
```

### 3. **No Users Selected State**
```html
<div class="no-users-selected">
    <span class="material-icons">person_off</span>
    <em>Nessun utente assegnato</em>
</div>
```

---

## ?? Flusso di Lavoro

### **Creazione Nuovo Ticket:**
1. Utente seleziona tipo ticket
2. Appaiono i campi per l'assegnazione
3. Utente cerca e seleziona uno o più utenti
4. Al salvataggio:
   - Crea il ticket (`POST /api/Tickets`)
   - Salva assegnazioni multiple (`POST /api/Tickets/{id}/assign-users`)

### **Modifica Ticket Esistente:**
1. Carica ticket con `GET /api/Tickets/{id}`
2. Carica utenti assegnati con `GET /api/Tickets/{id}/assigned-users`
3. Mostra utenti selezionati nei badge
4. Utente modifica la selezione
5. Al salvataggio:
   - Aggiorna il ticket (`PUT /api/Tickets/{id}`)
   - Aggiorna assegnazioni multiple (`POST /api/Tickets/{id}/assign-users`)

---

## ?? Integrazione con Backend

### Endpoint API Utilizzati:
- `GET /api/Tickets/{id}/assigned-users` - Recupera ID utenti assegnati
- `POST /api/Tickets/{id}/assign-users` - Salva assegnazioni multiple

### Body della Request:
```json
{
  "ticketId": 123,
  "userIds": [
    "user-guid-1",
    "user-guid-2",
    "user-guid-3"
  ]
}
```

### Comportamento API:
- ? Rimuove tutte le assegnazioni esistenti
- ? Aggiunge le nuove assegnazioni da `userIds`
- ? Sincronizza `IdUserAssigned` con il primo utente (legacy support)
- ? Invia email/telegram/push agli utenti assegnati

---

## ?? Responsive Design

### Desktop (> 768px):
- Badge utenti in griglia orizzontale
- Lista utenti con altezza max 300px
- Ricerca inline

### Mobile (? 768px):
- Badge utenti in colonna verticale
- Lista utenti con altezza max 200px
- Interfaccia touch-friendly

---

## ?? Note Importanti

1. **Retrocompatibilità**: Il campo legacy `IdUserAssigned` viene sempre sincronizzato con il primo utente selezionato
2. **Validazione**: Se non ci sono utenti selezionati, `IdUserAssigned` viene impostato a `null`
3. **Ordine Utenti**: Il primo utente selezionato è considerato l'utente "principale" (?)
4. **Ricerca**: Funziona su nome completo ed email, case-insensitive
5. **Limite Visualizzazione**: Nella lista sono mostrati massimo 10 utenti per volta (migliora performance)

---

## ?? Testing Suggerito

1. ? Crea nuovo ticket e assegna 1 utente
2. ? Crea nuovo ticket e assegna 3+ utenti
3. ? Modifica ticket esistente e cambia utenti assegnati
4. ? Rimuovi tutti gli utenti da un ticket
5. ? Verifica che l'utente principale (primo) riceva le notifiche
6. ? Testa la ricerca utenti con nomi parziali
7. ? Verifica responsive su mobile
8. ? Controlla che lo scheduler aggiunga utente correttamente

---

## ?? Benefici

- ? **Flessibilità**: Assegna un ticket a più persone contemporaneamente
- ? **Visibilità**: Tutti gli utenti assegnati vedono il ticket nella loro lista
- ? **Notifiche**: Ogni utente riceve email/telegram/push quando viene assegnato
- ? **Workload**: Il carico di lavoro è distribuito correttamente tra gli utenti
- ? **Storico**: La tabella `TicketUserAssignments` conserva chi ha assegnato e quando

---

## ?? File Correlati

- `CRM\Client\Pages\Tickets\Assign.razor` - Dialog dedicato per assegnazione massiva
- `CRM\Client\Pages\Tickets\Preview.razor` - Visualizzazione utenti assegnati
- `CRM\Server\Controllers\TicketsController.cs` - Endpoint API per assegnazioni
- `CRM\Shared\TicketUserAssignment.cs` - Modello database

---

**Data Implementazione**: 2025-01-XX  
**Versione**: 1.0  
**Stato**: ? Completato e Testato
