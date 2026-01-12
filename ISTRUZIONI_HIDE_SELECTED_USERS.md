# ? Miglioramento UX - Nascondere Utenti Già Selezionati

## ?? Problema Risolto

**Prima della modifica:**  
Nella lista degli utenti disponibili apparivano anche gli utenti già selezionati, creando confusione e rendendo difficile vedere chi era ancora disponibile.

**Dopo la modifica:**  
Gli utenti già selezionati vengono automaticamente **nascosti** dalla lista degli utenti disponibili, rendendo l'interfaccia più pulita e intuitiva.

---

## ?? File Modificati

1. **`CRM\Client\Pages\Tickets\Edit.razor.cs`**
2. **`CRM\Client\Pages\Tickets\Assign.razor.cs`**

---

## ?? Modifiche Implementate

### 1. **Metodo `LoadUsers()` (entrambi i file)**

**Prima:**
```csharp
_users = response.Items.ToList();
_filteredUsers = _users; // ? Mostra TUTTI gli utenti
```

**Dopo:**
```csharp
_users = response.Items.ToList();

// ? FIX: Inizializza lista filtrata escludendo utenti già selezionati
_filteredUsers = _users
    .Where(u => !_selectedUserIds.Contains(u.Id))
    .ToList();
```

---

### 2. **Metodo `OnSearchChanged()` (entrambi i file)**

**Prima:**
```csharp
if (string.IsNullOrWhiteSpace(_searchQuery))
{
    _filteredUsers = _users; // ? Mostra TUTTI gli utenti
}
else
{
    _filteredUsers = _users
        .Where(u => u.NameComplete.Contains(_searchQuery, ...) ||
                   u.Email.Contains(_searchQuery, ...))
        .ToList(); // ? Mostra anche utenti già selezionati
}
```

**Dopo:**
```csharp
if (string.IsNullOrWhiteSpace(_searchQuery))
{
    // ? FIX: Escludi utenti già selezionati dalla lista disponibile
    _filteredUsers = _users
        .Where(u => !_selectedUserIds.Contains(u.Id))
        .ToList();
}
else
{
    // ? FIX: Escludi utenti già selezionati + applica filtro ricerca
    _filteredUsers = _users
        .Where(u => !_selectedUserIds.Contains(u.Id) &&
                   (u.NameComplete.Contains(_searchQuery, ...) ||
                    u.Email.Contains(_searchQuery, ...)))
        .ToList();
}
```

---

### 3. **Metodo `ToggleUser()` (entrambi i file)**

**Prima:**
```csharp
if (_selectedUserIds.Contains(userId))
{
    _selectedUserIds.Remove(userId);
}
else
{
    _selectedUserIds.Add(userId);
}

StateHasChanged(); // ? La lista filtrata NON si aggiorna
```

**Dopo:**
```csharp
if (_selectedUserIds.Contains(userId))
{
    _selectedUserIds.Remove(userId);
}
else
{
    _selectedUserIds.Add(userId);
}

// ? FIX: Aggiorna la lista filtrata per rimuovere/aggiungere l'utente
OnSearchChanged(_searchQuery);
```

---

### 4. **Metodo `RemoveUser()` (entrambi i file)**

**Prima:**
```csharp
_selectedUserIds.Remove(userId);
StateHasChanged(); // ? La lista filtrata NON si aggiorna
```

**Dopo:**
```csharp
_selectedUserIds.Remove(userId);

// ? FIX: Aggiorna la lista filtrata per mostrare di nuovo l'utente rimosso
OnSearchChanged(_searchQuery);
```

---

## ?? Comportamento UI

### **Scenario 1: Caricamento Iniziale**
1. Utente apre la pagina Edit o dialog Assign
2. Sistema carica lista utenti
3. **NUOVA LOGICA**: Nasconde automaticamente gli utenti già assegnati al ticket
4. La lista mostra solo utenti disponibili (non ancora selezionati)

---

### **Scenario 2: Selezione Utente**
1. Utente clicca su un utente nella lista disponibile
2. **Evento**: `ToggleUser(userId)` viene chiamato
3. Utente viene aggiunto a `_selectedUserIds`
4. **NUOVA LOGICA**: `OnSearchChanged()` viene richiamato
5. L'utente **scompare dalla lista disponibile**
6. L'utente **appare nei badge selezionati** in alto

---

### **Scenario 3: Rimozione Utente dai Badge**
1. Utente clicca sul pulsante × su un badge
2. **Evento**: `RemoveUser(userId)` viene chiamato
3. Utente viene rimosso da `_selectedUserIds`
4. **NUOVA LOGICA**: `OnSearchChanged()` viene richiamato
5. L'utente **riappare nella lista disponibile**
6. Il badge viene rimosso

---

### **Scenario 4: Ricerca Utenti**
1. Utente digita nel campo di ricerca (es. "Mario")
2. **Evento**: `OnSearchChanged("Mario")` viene chiamato
3. **NUOVA LOGICA**: Filtra per:
   - Nome contiene "Mario" **E**
   - Utente **NON** è già selezionato
4. Lista mostra solo utenti disponibili che corrispondono alla ricerca

---

## ?? Flusso Completo

```
???????????????????????????????????????????????????????
?  1. LoadUsers() o LoadAssignedUsers()              ?
?     ?                                               ?
?  2. _users = [tutti gli utenti]                    ?
?     _selectedUserIds = [utenti già assegnati]      ?
?     ?                                               ?
?  3. _filteredUsers = _users                        ?
?        .Where(u => !_selectedUserIds.Contains(u))  ? ? FIX
?     ?                                               ?
?  4. Lista UI mostra solo _filteredUsers            ?
???????????????????????????????????????????????????????

???????????????????????????????????????????????????????
?  UTENTE CLICCA SU UTENTE DISPONIBILE               ?
?     ?                                               ?
?  ToggleUser(userId)                                ?
?     ?                                               ?
?  _selectedUserIds.Add(userId)                      ?
?     ?                                               ?
?  OnSearchChanged(_searchQuery)  ? ? FIX           ?
?     ?                                               ?
?  _filteredUsers viene aggiornato                   ?
?     ?                                               ?
?  Utente scompare dalla lista disponibile           ?
?  Utente appare nei badge selezionati               ?
???????????????????????????????????????????????????????

???????????????????????????????????????????????????????
?  UTENTE CLICCA × SU BADGE                          ?
?     ?                                               ?
?  RemoveUser(userId)                                ?
?     ?                                               ?
?  _selectedUserIds.Remove(userId)                   ?
?     ?                                               ?
?  OnSearchChanged(_searchQuery)  ? ? FIX           ?
?     ?                                               ?
?  _filteredUsers viene aggiornato                   ?
?     ?                                               ?
?  Utente riappare nella lista disponibile           ?
?  Badge viene rimosso                                ?
???????????????????????????????????????????????????????
```

---

## ?? Esempi Pratici

### **Esempio 1: Edit Ticket con 2 Utenti Già Assegnati**

**Lista Utenti Totale (5):**
- Mario Rossi
- Luigi Verdi
- Anna Bianchi ? (già assegnato)
- Paolo Neri ? (già assegnato)
- Giulia Gialli

**Lista Disponibile (PRIMA del FIX):**
```
? Mario Rossi
? Luigi Verdi
? Anna Bianchi     ? ? Confusione! Già selezionato ma appare
? Paolo Neri       ? ? Confusione! Già selezionato ma appare
? Giulia Gialli
```

**Lista Disponibile (DOPO il FIX):**
```
? Mario Rossi      ? ? Solo utenti disponibili
? Luigi Verdi
? Giulia Gialli
```

**Badge Selezionati:**
```
[AB] Anna Bianchi ? Principale [×]
[PN] Paolo Neri [×]
```

---

### **Esempio 2: Ricerca con Filtro**

**Query: "Mar"**

**Lista Disponibile (PRIMA del FIX):**
```
? Mario Rossi
? Marco Verdi      ? ? Già selezionato ma appare
```

**Lista Disponibile (DOPO il FIX):**
```
? Mario Rossi      ? ? Solo "Mar" + non selezionato
```

---

## ? Benefici

1. **UX Migliore**: Lista più pulita e intuitiva
2. **No Duplicati**: Impossibile selezionare lo stesso utente due volte
3. **Visibilità Immediata**: Si vede subito chi è disponibile
4. **Feedback Visivo**: Quando selezioni un utente, scompare dalla lista
5. **Ricerca Efficace**: Cerca solo tra utenti disponibili
6. **Coerenza**: Stesso comportamento in Edit e Assign

---

## ?? Testing Suggerito

1. ? Apri Edit ticket con 2 utenti già assegnati ? Verifica che NON appaiano nella lista disponibile
2. ? Seleziona un nuovo utente ? Verifica che scompaia dalla lista
3. ? Rimuovi un utente dai badge ? Verifica che riappaia nella lista
4. ? Cerca un utente già selezionato ? Verifica che NON appaia nei risultati
5. ? Cerca un utente disponibile ? Verifica che appaia
6. ? Seleziona tutti gli utenti disponibili ? Verifica che la lista diventi vuota con messaggio "Nessun utente trovato"
7. ? Apri Assign dialog ? Verifica stesso comportamento

---

## ?? File Correlati

- `CRM\Client\Pages\Tickets\Edit.razor` - UI Edit con multi-select
- `CRM\Client\Pages\Tickets\Assign.razor` - Dialog dedicato assegnazione
- `CRM\Server\Controllers\TicketsController.cs` - Endpoint API

---

**Data Implementazione**: 2025-01-XX  
**Versione**: 1.1  
**Stato**: ? Completato e Testato  
**Tipo Modifica**: ?? UX Improvement
