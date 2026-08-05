using CRM.Mobile.Models;
using CRM.Mobile.Services;

namespace CRM.Mobile.Pages;

/// <summary>
/// La cattura allo stand: trenta secondi, in piedi, una mano occupata.
/// <para>
/// Tutto qui dentro e' subordinato a quel vincolo. La fiera si sceglie da un elenco (nessuno
/// conosce a memoria un id), la foto si scatta con un tocco, e il salvataggio non porta da
/// nessuna parte perche' chi registra ha altri dieci biglietti in mano.
/// </para>
/// </summary>
public sealed class CapturePage : ContentPage
{
    private readonly AppSettingsStore _settingsStore;
    private readonly InitiativeCatalog _catalog;
    private readonly LeadQueueStore _queue;
    private readonly LeadSyncService _sync;
    private readonly CrmApiClient _api;

    private readonly Picker _initiative = new() { Title = "Scegli la fiera", FontSize = 16 };
    private readonly Entry _name = new() { Placeholder = "Nome" };
    private readonly Entry _company = new() { Placeholder = "Azienda" };
    private readonly Entry _jobTitle = new() { Placeholder = "Ruolo" };
    private readonly Entry _phone = new() { Placeholder = "Telefono", Keyboard = Keyboard.Telephone };
    private readonly Entry _email = new() { Placeholder = "Email", Keyboard = Keyboard.Email };
    private readonly Editor _note = new() { Placeholder = "Cosa voleva — due parole bastano", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 84 };

    private readonly Image _preview = new() { Aspect = Aspect.AspectFit, HeightRequest = 180, BackgroundColor = Color.FromArgb("#EEF2F4") };
    private readonly Label _status = new() { TextColor = Theme.Muted, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label _pending = new() { TextColor = Theme.Muted, FontAttributes = FontAttributes.Bold };
    private readonly Label _sessionCount = new() { TextColor = Theme.Muted };
    private readonly Button _saveButton;

    private readonly Button _hot;
    private readonly Button _warm;
    private readonly Button _cold;
    private int _heat = 1;

    private List<FieldInitiative> _initiatives = new();
    private string? _photoPath;
    private string? _photoFileName;
    private bool _photoNeedsOcr;
    private int _savedInSession;

    public CapturePage(
        AppSettingsStore settingsStore,
        InitiativeCatalog catalog,
        LeadQueueStore queue,
        LeadSyncService sync,
        CrmApiClient api)
    {
        _settingsStore = settingsStore;
        _catalog = catalog;
        _queue = queue;
        _sync = sync;
        _api = api;

        Title = "Cattura biglietti";
        BackgroundColor = Theme.Page;

        _hot = HeatButton("Caldo", 0);
        _warm = HeatButton("Tiepido", 1);
        _cold = HeatButton("Freddo", 2);
        _saveButton = Theme.PrimaryButton("Salva e avanti", SaveAsync);

        _initiative.SelectedIndexChanged += async (_, _) => await RememberInitiativeAsync();
        _sync.Synced += (_, result) => MainThread.BeginInvokeOnMainThread(() => ShowPending(result.Pending));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 18, 18, 28),
                Spacing = 14,
                Children =
                {
                    Theme.Card(_initiative, Theme.Hint("La fiera si sceglie una volta: resta impostata anche riaprendo l'app.")),

                    Theme.Card(
                        _preview,
                        Theme.PrimaryButton("Fotografa il biglietto", CapturePhotoAsync),
                        Theme.SecondaryButton("Scegli una foto già scattata", PickPhotoAsync),
                        _status),

                    Theme.SectionTitle("Contatto"),
                    Theme.Card(_name, _company, _jobTitle, _phone, _email),

                    Theme.SectionTitle("Cosa voleva"),
                    Theme.Card(
                        _note,
                        Theme.Hint("È l'unica cosa che stasera non ricostruisci: nome e azienda stanno sul cartoncino."),
                        HeatRow()),

                    _saveButton,

                    Theme.Card(_pending, _sessionCount, Theme.SecondaryButton("Invia ora", FlushAsync))
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var settings = await _settingsStore.LoadAsync();
        if (!settings.IsComplete)
        {
            SetStatus("Prima imposta indirizzo e chiave nella scheda Configurazione.", Theme.Warn);
            return;
        }

        await LoadInitiativesAsync();
        ShowPending(await _queue.CountAsync());

        // Aprire l'app e' gia' un tentativo di svuotare la coda: se la rete e' tornata durante la
        // notte, i biglietti di ieri partono senza che nessuno debba ricordarsene.
        await _sync.FlushAsync();
    }

    private async Task LoadInitiativesAsync()
    {
        var (items, fromCache, error) = await _catalog.GetAsync();
        _initiatives = items;
        _initiative.ItemsSource = items;

        if (items.Count == 0)
        {
            SetStatus(error ?? "Nessuna iniziativa disponibile: controlla la configurazione.", Theme.Warn);
            return;
        }

        var settings = await _settingsStore.LoadAsync();
        var index = items.FindIndex(x => x.Id == settings.LastInitiativeId);
        if (index < 0)
            index = items.FindIndex(x => x.IsCurrent);

        _initiative.SelectedIndex = index >= 0 ? index : 0;

        if (fromCache)
            SetStatus("Elenco fiere non aggiornato (nessuna rete): sto usando l'ultimo scaricato.", Theme.Muted);
    }

    private async Task RememberInitiativeAsync()
    {
        if (_initiative.SelectedItem is FieldInitiative selected)
            await _settingsStore.RememberInitiativeAsync(selected.Id);
    }

    // ---- foto -------------------------------------------------------------------------

    private async Task CapturePhotoAsync()
    {
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            SetStatus("Permesso fotocamera negato: puoi comunque scegliere una foto dalla galleria.", Theme.Warn);
            return;
        }

        if (!MediaPicker.Default.IsCaptureSupported)
        {
            SetStatus("Questo dispositivo non espone la fotocamera all'app.", Theme.Warn);
            return;
        }

        var file = await MediaPicker.Default.CapturePhotoAsync();
        if (file != null)
            await UsePhotoAsync(file);
    }

    private async Task PickPhotoAsync()
    {
        var file = await MediaPicker.Default.PickPhotoAsync();
        if (file != null)
            await UsePhotoAsync(file);
    }

    /// <summary>
    /// La foto si copia SUBITO in una cartella dell'app: il file restituito dalla fotocamera e'
    /// temporaneo e il sistema puo' ripulirlo, mentre un biglietto in coda deve poter aspettare
    /// giorni senza rete.
    /// </summary>
    private async Task UsePhotoAsync(FileResult file)
    {
        try
        {
            _photoFileName = string.IsNullOrWhiteSpace(file.FileName) ? "biglietto.jpg" : file.FileName;
            var extension = Path.GetExtension(_photoFileName);
            _photoPath = Path.Combine(FileSystem.AppDataDirectory, $"{Guid.NewGuid():N}{extension}");

            await using (var input = await file.OpenReadAsync())
            await using (var output = File.Create(_photoPath))
            {
                await input.CopyToAsync(output);
            }

            _preview.Source = ImageSource.FromFile(_photoPath);

            if (!CrmApiClient.IsOnline)
            {
                // Niente rete: la foto e' al sicuro e il CRM la leggera' al primo invio riuscito.
                _photoNeedsOcr = true;
                SetStatus("Nessuna rete: la foto è salvata, i campi li legge il CRM all'invio.", Theme.Muted);
                return;
            }

            _photoNeedsOcr = false;
            await AnalyzeAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Foto non acquisita: {ex.Message}. Puoi comunque compilare a mano.", Theme.Warn);
        }
    }

    private async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(_photoPath))
            return;

        SetStatus("Lettura del biglietto in corso...", Theme.Muted);
        var result = await _api.AnalyzeBusinessCardAsync(_photoPath, _photoFileName ?? "biglietto.jpg");

        if (!result.Success)
        {
            // Se non si e' potuto leggere adesso, ci riprova il CRM al momento dell'invio.
            _photoNeedsOcr = true;
            SetStatus(result.ErrorMessage ?? "Lettura non riuscita: compila a mano.", Theme.Warn);
            return;
        }

        // Solo i campi vuoti: quello che ha scritto la persona vince sempre.
        FillIfEmpty(_name, result.FullName);
        FillIfEmpty(_company, result.CompanyName);
        FillIfEmpty(_jobTitle, result.JobTitle);
        FillIfEmpty(_phone, result.Phone);
        FillIfEmpty(_email, result.Email);

        SetStatus("Campi letti dal biglietto: controllali con un'occhiata.", Theme.Ok);
    }

    // ---- salvataggio ------------------------------------------------------------------

    private async Task SaveAsync()
    {
        if (_initiative.SelectedItem is not FieldInitiative fiera)
        {
            SetStatus("Scegli prima la fiera.", Theme.Warn);
            return;
        }

        var label = FirstFilled(_name.Text, _company.Text, _email.Text, _phone.Text);
        if (label == null && string.IsNullOrWhiteSpace(_photoPath))
        {
            SetStatus("Fotografa il biglietto o scrivi almeno un dato.", Theme.Warn);
            return;
        }

        _saveButton.IsEnabled = false;
        try
        {
            await _queue.EnqueueAsync(new PendingLead
            {
                Name = label ?? $"Biglietto delle {DateTime.Now:HH:mm}",
                CompanyName = Clean(_company.Text),
                JobTitle = Clean(_jobTitle.Text),
                Phone = Clean(_phone.Text),
                Email = Clean(_email.Text),
                Note = Clean(_note.Text),
                Score = _heat switch { 0 => 80, 2 => 20, _ => 50 },
                InitiativeId = fiera.Id,
                InitiativeName = fiera.Name,
                PhotoPath = _photoPath,
                PhotoFileName = _photoFileName,
                NeedsOcr = _photoNeedsOcr,
                CreatedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            // La coda non ha accettato: il modulo NON si svuota, perche' in questo istante il
            // contatto esiste solo qui sullo schermo.
            SetStatus($"Biglietto non messo in coda ({ex.Message}). I dati sono ancora qui.", Theme.Danger);
            return;
        }
        finally
        {
            _saveButton.IsEnabled = true;
        }

        _savedInSession++;
        ResetForm();
        ShowPending(await _queue.CountAsync());
        SetStatus("Biglietto salvato. Avanti col prossimo.", Theme.Ok);

        await _sync.FlushAsync();
    }

    private async Task FlushAsync()
    {
        var result = await _sync.FlushAsync();
        ShowPending(result.Pending);

        SetStatus(
            result.Sent > 0
                ? $"{result.Sent} biglietti inviati al CRM."
                : result.Error ?? "Niente da inviare.",
            result.Error == null ? Theme.Ok : Theme.Warn);
    }

    // ---- interfaccia ------------------------------------------------------------------

    private View HeatRow()
    {
        var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        grid.Add(_hot, 0);
        grid.Add(_warm, 1);
        grid.Add(_cold, 2);
        return grid;
    }

    private Button HeatButton(string text, int value)
    {
        var button = new Button
        {
            Text = text,
            CornerRadius = 10,
            MinimumHeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };

        button.Clicked += (_, _) =>
        {
            _heat = value;
            PaintHeat();
        };

        return button;
    }

    private void PaintHeat()
    {
        Paint(_hot, 0, Theme.Danger);
        Paint(_warm, 1, Theme.Warn);
        Paint(_cold, 2, Color.FromArgb("#0284C7"));

        void Paint(Button button, int value, Color color)
        {
            var selected = _heat == value;
            button.BackgroundColor = selected ? color : Colors.White;
            button.TextColor = selected ? Colors.White : Theme.Muted;
            button.BorderColor = selected ? color : Theme.Line;
            button.BorderWidth = 1;
        }
    }

    private void ShowPending(int pending)
    {
        _pending.Text = pending == 0
            ? "Nessun biglietto in attesa: è tutto nel CRM."
            : pending == 1 ? "1 biglietto da inviare" : $"{pending} biglietti da inviare";

        _pending.TextColor = pending == 0 ? Theme.Ok : Theme.Warn;
        _sessionCount.Text = $"Raccolti in questa sessione: {_savedInSession}";
    }

    private void ResetForm()
    {
        _name.Text = _company.Text = _jobTitle.Text = _phone.Text = _email.Text = string.Empty;
        _note.Text = string.Empty;
        _heat = 1;
        PaintHeat();

        // Il percorso si azzera ma il file NON si cancella: da qui in poi appartiene alla coda.
        _photoPath = null;
        _photoFileName = null;
        _photoNeedsOcr = false;
        _preview.Source = null;
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.TextColor = color;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        PaintHeat();
    }

    private static void FillIfEmpty(InputView target, string? value)
    {
        if (string.IsNullOrWhiteSpace(target.Text) && !string.IsNullOrWhiteSpace(value))
            target.Text = value.Trim();
    }

    private static string? FirstFilled(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
