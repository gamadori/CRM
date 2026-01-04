# LocalizationService

## Descrizione

`LocalizationService` è un servizio che fornisce funzionalità di localizzazione **case-insensitive** per l'applicazione CRM. Consente di recuperare stringhe localizzate dai file `.resx` senza preoccuparsi della capitalizzazione esatta della chiave.

## Caratteristiche

- ? Ricerca **case-insensitive** delle chiavi di localizzazione
- ? Fallback automatico alla chiave originale se la risorsa non viene trovata
- ? Supporto per tutte le culture configurate (en-US, it-IT, es-ES, fr-FR)
- ? Performance ottimizzate con ricerca esatta prima del fallback case-insensitive

## Registrazione

Il servizio è già registrato nel DI container in `Program.cs`:

```csharp
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
```

## Utilizzo

### Iniettare il servizio

```csharp
public class MyComponent : ComponentBase
{
    [Inject]
    private ILocalizationService LocalizationService { get; set; }
    
    // ...
}
```

### Esempi di utilizzo

```csharp
// Tutte queste chiamate restituiscono lo stesso risultato
// anche se la chiave effettiva in App.resx è "Companies"
var text1 = LocalizationService.GetLocalizedString("companies");
var text2 = LocalizationService.GetLocalizedString("COMPANIES");
var text3 = LocalizationService.GetLocalizedString("Companies");
var text4 = LocalizationService.GetLocalizedString("CoMpAnIeS");

// Verificare se una risorsa esiste
if (!LocalizationService.IsResourceNotFound("products"))
{
    var productText = LocalizationService.GetLocalizedString("products");
}
```

### Utilizzo in HeaderService

`HeaderService` utilizza già `LocalizationService` internamente:

```csharp
public class HeaderService : IHeaderService
{
    private readonly ILocalizationService _localizationService;
    
    public HeaderService(ILocalizationService localizationService, ...)
    {
        _localizationService = localizationService;
    }
    
    private string GetLocalizedString(string key) 
        => _localizationService.GetLocalizedString(key);
}
```

## File di risorse supportati

Il servizio legge dalle seguenti risorse:
- `CRM.Shared.Resources.App.resx` (default: en-US)
- `CRM.Shared.Resources.App.it.resx` (italiano)
- `CRM.Shared.Resources.App.es.resx` (spagnolo)
- `CRM.Shared.Resources.App.fr.resx` (francese)

## Performance

1. **Prima ricerca**: Exact match (veloce)
2. **Seconda ricerca**: Case-insensitive match (solo se exact match fallisce)
3. **Fallback**: Restituisce la chiave originale se non trova nulla

## Note

- Il servizio è **thread-safe**
- Utilizza `StringComparison.OrdinalIgnoreCase` per il confronto case-insensitive
- Include culture parent nella ricerca (`includeParentCultures: true`)

## Esempio completo

```csharp
@page "/example"
@inject ILocalizationService Localization

<h1>@Localization.GetLocalizedString("companies")</h1>

@code {
    protected override void OnInitialized()
    {
        // Tutte queste chiavi funzioneranno
        var companies = Localization.GetLocalizedString("companies");
        var products = Localization.GetLocalizedString("PRODUCTS");
        var articles = Localization.GetLocalizedString("Articles");
        
        // Verifica esistenza risorsa
        if (!Localization.IsResourceNotFound("contacts"))
        {
            var contacts = Localization.GetLocalizedString("contacts");
        }
    }
}
```
