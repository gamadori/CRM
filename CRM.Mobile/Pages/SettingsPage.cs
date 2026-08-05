using CRM.Mobile.Models;
using CRM.Mobile.Services;

namespace CRM.Mobile.Pages;

/// <summary>
/// Configurazione del collegamento al CRM.
/// <para>
/// Pagina separata dalla cattura di proposito: si compila una volta, in ufficio, e allo stand non
/// si tocca piu'. Tenerla nella stessa schermata dei biglietti significa avere sotto le dita un
/// campo "indirizzo del server" mentre si ha davanti una persona - e prima o poi si cancella.
/// </para>
/// </summary>
public sealed class SettingsPage : ContentPage
{
    private readonly AppSettingsStore _settingsStore;
    private readonly CrmApiClient _api;
    private readonly InitiativeCatalog _catalog;

    private readonly Entry _apiUrl = new() { Placeholder = "https://crm.azienda.it", Keyboard = Keyboard.Url };
    private readonly Entry _apiKey = new() { Placeholder = "crmfd_...", IsPassword = true };
    private readonly Label _status = new() { TextColor = Theme.Muted, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _verify;

    public SettingsPage(AppSettingsStore settingsStore, CrmApiClient api, InitiativeCatalog catalog)
    {
        _settingsStore = settingsStore;
        _api = api;
        _catalog = catalog;

        Title = "Configurazione";
        BackgroundColor = Theme.Page;
        IconImageSource = "settings.png";

        _verify = Theme.PrimaryButton("Salva e verifica", SaveAndVerifyAsync);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 18, 18, 28),
                Spacing = 14,
                Children =
                {
                    Theme.SectionTitle("Collegamento al CRM"),
                    Theme.Card(
                        Theme.FieldLabel("Indirizzo del CRM"),
                        _apiUrl,
                        Theme.Hint("Lo stesso indirizzo che usi dal browser, senza pagine: https://crm.azienda.it"),
                        Theme.FieldLabel("Chiave dell'app"),
                        _apiKey,
                        Theme.Hint("La genera un amministratore dal CRM, in Impostazioni → Chiavi app fiera. Si vede una volta sola."),
                        _verify,
                        _status),

                    Theme.SectionTitle("Come funziona"),
                    Theme.Card(
                        Theme.Hint(
                            "I biglietti vengono salvati sul telefono e inviati appena c'è rete. " +
                            "Se allo stand non prende, continua a raccogliere: partono da soli quando torna il campo."))
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var settings = await _settingsStore.LoadAsync();
        _apiUrl.Text = settings.ApiBaseUrl;
        _apiKey.Text = settings.ApiKey;

        if (!settings.IsComplete)
            SetStatus("Indirizzo e chiave non ancora impostati.", Theme.Warn);
    }

    /// <summary>
    /// Salva e verifica in un colpo solo: una configurazione salvata ma mai provata e' quella che
    /// si scopre sbagliata in fiera, che e' esattamente il momento peggiore.
    /// </summary>
    private async Task SaveAndVerifyAsync()
    {
        var url = (_apiUrl.Text ?? string.Empty).Trim();
        var key = (_apiKey.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Servono sia l'indirizzo sia la chiave.", Theme.Warn);
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("L'indirizzo deve iniziare con http:// o https://", Theme.Warn);
            return;
        }

        var current = await _settingsStore.LoadAsync();
        await _settingsStore.SaveAsync(new AppSettings
        {
            ApiBaseUrl = url,
            ApiKey = key,
            LastInitiativeId = current.LastInitiativeId
        });

        _verify.IsEnabled = false;
        SetStatus("Verifica in corso...", Theme.Muted);

        try
        {
            var (ok, message) = await _api.PingAsync();
            SetStatus(message, ok ? Theme.Ok : Theme.Warn);

            // Riuscita la verifica si scarica subito l'elenco delle fiere: cosi' la copia locale
            // c'e' gia' prima di partire, e allo stand la tendina funziona anche senza rete.
            if (ok)
            {
                var (items, _, _) = await _catalog.GetAsync(forceRefresh: true);
                SetStatus($"{message}\n{items.Count} iniziative scaricate per l'uso offline.", Theme.Ok);
            }
        }
        finally
        {
            _verify.IsEnabled = true;
        }
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.TextColor = color;
    }
}
