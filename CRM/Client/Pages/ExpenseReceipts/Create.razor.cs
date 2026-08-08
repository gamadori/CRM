using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Create : ComponentBase
    {
        /// <summary>
        /// Null quando si arriva dall'elenco globale: in quel caso il contesto lo sceglie
        /// l'operatore nel form, e "nessun contesto" e' una scelta valida.
        /// </summary>
        [Parameter] public int? InterventionId { get; set; }

        /// <summary>
        /// Valorizzato quando si arriva dalla visita in agenda. Alternativo all'intervento: sono
        /// due rotte diverse, e nessuna delle due chiede all'operatore di sapere un numero.
        /// </summary>
        [Parameter] public int? ActivityId { get; set; }

        /// <summary>
        /// Iniziativa di provenienza ("/ExpenseReceipts/Create?idInitiative=12"): registrando la
        /// spesa dal resoconto della trasferta, il contesto sta nel percorso e non si digita.
        /// Resta comunque scegliibile a mano, perche' la spesa di una fiera si registra spesso
        /// dall'elenco generale, giorni dopo.
        /// </summary>
        [SupplyParameterFromQuery(Name = "idInitiative")]
        public int? InitiativeParam { get; set; }

        /// <summary>
        /// Iniziativa nel PERCORSO ("/Initiatives/12/ExpenseReceipts/Create"), cioe' quando si
        /// registra la spesa dalla cartella della fiera. Vale come l'intervento: il contesto non
        /// si digita e il ritorno e' il posto da cui si e' partiti.
        /// </summary>
        [Parameter] public int? InitiativeId { get; set; }

        /// <summary>
        /// L'iniziativa di riferimento, da qualunque delle due strade arrivi. Il percorso vince
        /// sulla query string: se ci sono entrambi, quello che si ha davanti e' il percorso.
        /// </summary>
        private int? InitiativeContextId => InitiativeId ?? InitiativeParam;

        /// <summary>Iniziative selezionabili: recenti e future (vedi <see cref="LoadInitiativesAsync"/>).</summary>
        private List<InitiativeDTO> _initiatives = new();

        /// <summary>Visita di riferimento; null su ogni altra rotta.</summary>
        private ActivityDTO _activity;

        /// <summary>Oggetto della visita, per intestare la pagina con qualcosa di leggibile.</summary>
        private string _activitySubject;

        /// <summary>Etichetta della visita: l'oggetto se lo conosciamo, altrimenti il numero.</summary>
        private string ActivityLabel => string.IsNullOrWhiteSpace(_activitySubject)
            ? $"Attivita #{ActivityId}"
            : _activitySubject;

        /// <summary>Nome dell'iniziativa scelta, per intestare la pagina con qualcosa di leggibile.</summary>
        private string InitiativeLabel =>
            _initiatives.FirstOrDefault(i => i.Id == _model.IdInitiative)?.Name ?? $"Iniziativa #{_model.IdInitiative}";

        private string ContextTitle =>
            InterventionId.HasValue ? $"Spesa dell'intervento #{InterventionId}"
            : ActivityId.HasValue ? $"Spesa della visita: {ActivityLabel}"
            : _model.IdInitiative.HasValue ? $"Spesa di: {InitiativeLabel}"
            : "Costo generale";

        private string CancelUrl =>
            InterventionId.HasValue ? $"/TicketInterventions/{InterventionId}/ExpenseReceipts"
            : ActivityId.HasValue ? $"/Activities/{ActivityId}/Info?view=notespese"
            // Si torna alla scheda dell'iniziativa GIA' APERTA sulle note spese: e' il posto da
            // cui si e' partiti. Prima si atterrava sui dettagli e la cartella andava ritrovata.
            : InitiativeContextId.HasValue ? $"/Initiatives/{InitiativeContextId}/Info?view=notespese"
            : "/ExpenseReceipts";

        private ExpenseReceiptCreateUpdateDTO _model = new();
        private ReceiptExtractionResult _extractionResult;
        private InputFile _inputFile;

        private bool _isLoading = false;
        private bool _isProcessing = false;
        private bool _isSaving = false;
        private string _errorMessage;

        private string _previewDataUrl;
        private string _previewContentType;
        private string _previewFileName;
        private int? _uploadedAttachmentFileId;

        /// <summary>Valute compatibili col simbolo letto, quando il codice ISO non c'e'.</summary>
        private List<string> _currencyCandidates = new();

        /// <summary>Persone selezionabili come chi ha sostenuto la spesa.</summary>
        private List<ApplicationUser> _users = new();
        private string _baseCurrency = "EUR";
        private bool _loadingRate;
        private bool _rateUnavailable;

        /// <summary>
        /// Vero quando la valuta non c'e'. E' lo stato che merita di essere gridato: senza valuta
        /// non si puo' convertire, e senza conversione la spesa non entra in nessun totale.
        /// </summary>
        private bool CurrencyMissing => string.IsNullOrWhiteSpace(_model.Currency);

        /// <summary>
        /// Un file che contiene piu' documenti fiscali diventa piu' note spese, una per documento:
        /// e' quello che fa il server, e la maschera deve mostrare la stessa cosa. Finche' mostrava
        /// un solo blocco di campi aggregati, si rivedeva un esercente "A / B" e un totale sommato
        /// che non venivano salvati da nessuna parte.
        /// </summary>
        private bool IsMultiDocument => _model.Documents?.Count > 1;

        /// <summary>
        /// Valuta da propagare con un clic: quella scritta a mano se c'e', altrimenti quella
        /// riconosciuta sul file, altrimenti quella di casa.
        /// </summary>
        private string CurrencyToApply =>
            !string.IsNullOrWhiteSpace(_model.Currency) ? _model.Currency.Trim().ToUpperInvariant()
            : !string.IsNullOrWhiteSpace(_extractionResult?.Currency) ? _extractionResult.Currency
            : _baseCurrency;

        /// <summary>
        /// Tipo dichiarato da chi carica. Riguarda solo i PDF: senza dichiarazione il file viene
        /// analizzato con tutti e due i modelli, perche' una fattura e una scansione di scontrini
        /// dall'esterno non si distinguono. Dichiararlo dimezza il costo dell'analisi.
        /// </summary>
        private ReceiptDocumentKind _kind = ReceiptDocumentKind.Unknown;

        /// <summary>Chiave con cui la scelta resta fra un caricamento e l'altro.</summary>
        private const string KindStorageKey = "crm-expense-document-kind";

        private string KindButtonClass(ReceiptDocumentKind kind)
            => _kind == kind ? "btn btn-primary" : "btn btn-outline-secondary";

        private string KindHint => _kind switch
        {
            ReceiptDocumentKind.Invoice =>
                "Un documento solo, anche se occupa più pagine. Ne nasce una nota spese.",
            ReceiptDocumentKind.Receipts =>
                "Uno o più scontrini: ognuno diventa una nota spese a sé.",
            _ =>
                "Se lo indichi, il PDF viene analizzato una volta sola invece di due. "
                + "Per le foto non serve: sono sempre scontrini."
        };

        /// <summary>
        /// La scelta si ricorda: chi carica sempre fatture la imposta una volta e non ci pensa
        /// piu'. Se il salvataggio locale non e' disponibile si perde la memoria, non la funzione.
        /// </summary>
        private async Task SetKind(ReceiptDocumentKind kind)
        {
            _kind = kind;

            try
            {
                await JsRuntime.InvokeVoidAsync("localStorage.setItem", KindStorageKey, kind.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Preferenza tipo documento non memorizzata: {ex.Message}");
            }
        }

        private async Task LoadKindPreferenceAsync()
        {
            try
            {
                var stored = await JsRuntime.InvokeAsync<string>("localStorage.getItem", KindStorageKey);
                if (Enum.TryParse<ReceiptDocumentKind>(stored, out var kind))
                    _kind = kind;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Preferenza tipo documento non letta: {ex.Message}");
            }
        }

        /// <summary>Documenti tolti dal lotto, tenuti da parte finche' non si salva.</summary>
        private readonly List<ExpenseReceiptDocumentDTO> _excludedDocuments = new();

        private static string DescribeDocument(ExpenseReceiptDocumentDTO document)
            => string.IsNullOrWhiteSpace(document.MerchantName)
                ? $"#{document.SortOrder + 1}"
                : $"#{document.SortOrder + 1} {document.MerchantName}";

        /// <summary>
        /// Toglie un documento dal lotto: e' la pagina di continuazione che il filtro sul totale
        /// non ha intercettato, e l'unico che puo' saperlo e' chi ha il documento davanti.
        /// </summary>
        private void ExcludeDocument(ExpenseReceiptDocumentDTO document)
        {
            if (_model.Documents == null || _model.Documents.Count <= 1)
                return;

            _model.Documents.Remove(document);
            _excludedDocuments.Add(document);

            RenumberDocuments();
            PromoteSingleDocument();
        }

        private void RestoreExcludedDocuments()
        {
            if (_excludedDocuments.Count == 0)
                return;

            _model.Documents.AddRange(_excludedDocuments);
            _excludedDocuments.Clear();

            _model.Documents = _model.Documents
                .OrderBy(d => d.PageFrom ?? int.MaxValue)
                .ThenBy(d => d.SortOrder)
                .ToList();

            RenumberDocuments();
        }

        /// <summary>La numerazione delle schede resta 1..N: "Nota spese 3 di 2" non vuol dire niente.</summary>
        private void RenumberDocuments()
        {
            for (var i = 0; i < _model.Documents.Count; i++)
                _model.Documents[i].SortOrder = i;
        }

        /// <summary>
        /// Rimasto un documento solo, la maschera torna a quella singola: i campi in cima devono
        /// allora contenere QUEL documento, non piu' l'aggregato di quando erano due - altrimenti
        /// si salverebbe un esercente "A / B" e un totale sommato che non esiste piu'.
        /// </summary>
        private void PromoteSingleDocument()
        {
            if (_model.Documents.Count != 1)
                return;

            var document = _model.Documents[0];

            _model.MerchantName = document.MerchantName;
            _model.TransactionDate = document.TransactionDate;
            _model.TotalAmount = document.TotalAmount;
            _model.TaxAmount = document.TaxAmount;
            _model.Currency = document.Currency;
            _model.ExchangeRate = document.ExchangeRate;
            _model.Category = document.Category;
            _model.CategorySuggested = DocumentSuggestion(document);
            _model.CategorySource = document.CategorySource;
            _model.CategoryConfidence = document.CategoryConfidence;
            _model.CategoryReason = document.CategoryReason;
            _model.Description = $"{document.MerchantName} - {document.TransactionDate?.ToString("dd/MM/yyyy")}";
        }

        private void ApplyCurrencyToAllDocuments()
        {
            if (_model.Documents == null || string.IsNullOrWhiteSpace(CurrencyToApply))
                return;

            var currency = CurrencyToApply;

            // Si riempiono solo quelle vuote: se una valuta l'operatore l'ha gia' scelta, un
            // pulsante "applica a tutte" non deve portargliela via.
            foreach (var document in _model.Documents.Where(d => string.IsNullOrWhiteSpace(d.Currency)))
                document.Currency = currency;
        }

        /// <summary>
        /// Vero quando i dati arrivano da un documento letto. Cambia cosa si puo' dire all'utente:
        /// "non trovata sul documento" e' un'informazione, davanti a un inserimento a mano sarebbe
        /// una bugia, perche' documento non ce n'e'.
        /// </summary>
        private bool ReadFromDocument => _extractionResult?.Success == true;

        /// <summary>
        /// Cosa, salvando adesso, resterebbe fuori dai totali.
        /// <para>
        /// Sta in un punto solo perche' serve tre volte - il riquadro sopra il pulsante, il colore
        /// del pulsante e la conferma - e tre condizioni scritte tre volte divergono.
        /// </para>
        /// </summary>
        private List<string> ConversionWarnings
        {
            get
            {
                var warnings = new List<string>();

                // Con piu' documenti la valuta sta su ogni nota, non sul modulo: guardare quella
                // aggregata darebbe un avviso che non corrisponde a niente di salvato.
                if (IsMultiDocument)
                {
                    var documentsWithoutCurrency = DocumentsWithoutCurrency();
                    if (documentsWithoutCurrency.Count > 0)
                        warnings.Add($"{documentsWithoutCurrency.Count} note spese su {_model.Documents.Count} "
                                     + $"sono senza valuta: {string.Join(", ", documentsWithoutCurrency)}.");

                    return warnings;
                }

                if (CurrencyMissing && _model.TotalAmount.HasValue)
                    warnings.Add("La valuta non è indicata: la spesa resta «da convertire».");

                if (!CurrencyMissing && _needsRate && !_model.ExchangeRate.HasValue)
                    warnings.Add($"Manca il cambio da {_model.Currency?.Trim().ToUpperInvariant()} "
                                 + $"a {_baseCurrency}: la spesa resta «da convertire».");

                return warnings;
            }
        }

        /// <summary>
        /// Documenti dell'estrazione senza valuta, descritti in modo riconoscibile. Vuoto quando
        /// il documento e' uno solo: quel caso lo copre gia' <see cref="CurrencyMissing"/>.
        /// </summary>
        private List<string> DocumentsWithoutCurrency()
        {
            if (_model.Documents == null || _model.Documents.Count < 2)
                return new List<string>();

            return _model.Documents
                .Where(d => string.IsNullOrWhiteSpace(d.Currency) && d.TotalAmount.HasValue)
                .Select(d => string.IsNullOrWhiteSpace(d.MerchantName)
                    ? $"#{d.SortOrder + 1}"
                    : $"#{d.SortOrder + 1} {d.MerchantName}")
                .ToList();
        }

        /// <summary>
        /// Tipologia proposta per un documento del lotto. Serve al campo per capire se il valore
        /// che sta mostrando e' ancora la proposta o una correzione: sul documento la proposta
        /// non si conserva a parte, la si riconosce dal fatto che nessuno l'ha toccata.
        /// </summary>
        private static ExpenseCategory? DocumentSuggestion(ExpenseReceiptDocumentDTO document) =>
            document.CategorySource == ExpenseCategorySource.Manual ? null : document.Category;

        /// <summary>
        /// La tipologia scelta a mano smette di essere una proposta: si azzerano provenienza,
        /// confidenza e motivo, che parlavano di un valore che non c'e' piu'. Il server ricava
        /// comunque la provenienza per conto suo - qui si tiene solo la maschera coerente.
        /// </summary>
        private void OnCategoryChanged(ExpenseCategory? category)
        {
            _model.Category = category;
            _model.CategorySource = category.HasValue ? ExpenseCategorySource.Manual : null;
            _model.CategoryConfidence = null;
            _model.CategoryReason = null;
        }

        private static void OnDocumentCategoryChanged(ExpenseReceiptDocumentDTO document, ExpenseCategory? category)
        {
            document.Category = category;
            document.CategorySource = category.HasValue ? ExpenseCategorySource.Manual : null;
            document.CategoryConfidence = null;
            document.CategoryReason = null;
        }

        /// <summary>Il cambio serve solo se la spesa non e' gia' nella valuta di casa.</summary>
        private bool _needsRate =>
            !string.IsNullOrWhiteSpace(_model.Currency)
            && !string.Equals(_model.Currency.Trim(), _baseCurrency, StringComparison.OrdinalIgnoreCase);

        [Inject] IJSRuntime JsRuntime { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadKindPreferenceAsync();

            _model.TicketInterventionId = InterventionId;
            _model.IdActivity = ActivityId;
            _model.IdInitiative = InitiativeContextId;

            await LoadInitiativesAsync();

            // La data e' obbligatoria: una spesa senza data non e' collocabile in nessun periodo,
            // quindi si parte da oggi invece che da vuoto.
            _model.TransactionDate = DateTime.Today;

            _activity = await LoadActivityAsync();
            _activitySubject = _activity?.Subject;

            // La spesa di una visita porta la data della visita, non quella in cui la si registra:
            // lo scontrino della trasferta finisce in ufficio giorni dopo. Se la visita e' stata
            // completata vale la data in cui e' avvenuta, non quella in cui era prevista.
            var visitDate = _activity?.DoneDate ?? _activity?.DueDate;
            if (visitDate.HasValue)
                _model.TransactionDate = visitDate.Value.Date;

            _baseCurrency = await LoadBaseCurrencyAsync();
            _model.Currency = _baseCurrency;

            await LoadSpendersAsync();
        }

        /// <summary>
        /// Elenco delle persone e preselezione.
        /// <para>
        /// L'elenco resta completo anche su un intervento: puo' aver pagato qualcuno che non e'
        /// fra gli assegnati. Cambia la PRESELEZIONE — su un intervento si punta a chi ci sta
        /// lavorando, altrove a chi sta registrando.
        /// </para>
        /// </summary>
        private async Task LoadSpendersAsync()
        {
            try
            {
                var response = await _userService.Get(new UsersFilterModel { PageSize = 0 });
                _users = response?.Items?.OrderBy(u => u.NameComplete).ToList() ?? new List<ApplicationUser>();

                var me = await Http.GetFromJsonAsync<ApplicationUser>("api/Users/CurrentUser");
                var myId = me?.Id;

                _model.IdUserSpender = await PickDefaultSpenderAsync(myId);
            }
            catch (Exception ex)
            {
                // Senza elenco il campo resta vuoto e la validazione blocca il salvataggio:
                // meglio fermarsi che attribuire la spesa a qualcuno a caso.
                Console.WriteLine($"Errore caricamento persone: {ex.Message}");
            }
        }

        /// <summary>
        /// Iniziative fra cui scegliere: quelle che si sovrappongono all'ultimo anno o che devono
        /// ancora finire.
        /// <para>
        /// Non tutte, perche' uno scontrino non si attribuisce a una fiera di tre anni fa e un
        /// elenco lungo cosi' non si scorre; non solo quelle in corso, perche' gli scontrini di una
        /// trasferta arrivano in ufficio settimane dopo, a viaggio ormai chiuso. Se serve
        /// un'iniziativa piu' vecchia, la si assegna dalla modifica della nota spese.
        /// </para>
        /// </summary>
        private async Task LoadInitiativesAsync()
        {
            try
            {
                var items = await InitiativeService.GetListAsync(new InitiativeFilter
                {
                    DateFrom = DateTime.Today.AddYears(-1)
                });

                _initiatives = items ?? new List<InitiativeDTO>();
            }
            catch (Exception ex)
            {
                // Senza elenco resta un campo vuoto e la spesa si registra lo stesso: il contesto
                // e' facoltativo per costruzione, perderlo non deve impedire il rimborso.
                Console.WriteLine($"Iniziative non caricate: {ex.Message}");
            }
        }

        /// <summary>
        /// Recupera la visita per intestazione, data e persona di riferimento. Un errore qui non
        /// deve impedire di registrare la spesa: si perde la precompilazione, non la funzione.
        /// </summary>
        private async Task<ActivityDTO> LoadActivityAsync()
        {
            if (!ActivityId.HasValue)
                return null;

            try
            {
                return await Http.GetFromJsonAsync<ActivityDTO>($"api/Activities/{ActivityId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Visita non caricata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Su un intervento: chi registra, se e' fra gli assegnati; altrimenti il primo assegnato,
        /// che e' il caso dell'ufficio che inserisce la spesa del tecnico. Su una visita: chi ce
        /// l'ha in carico, con la stessa logica. Senza contesto: chi registra, perche' il caso
        /// normale e' "la spesa e' mia".
        /// </summary>
        private async Task<string> PickDefaultSpenderAsync(string currentUserId)
        {
            var fallback = _users.Any(u => u.Id == currentUserId) ? currentUserId : null;

            if (ActivityId.HasValue)
            {
                var owner = _activity?.IdAssignee ?? _activity?.IdUser;
                return _users.Any(u => u.Id == owner) ? owner : fallback;
            }

            if (!InterventionId.HasValue)
                return fallback;

            try
            {
                var assigned = await Http.GetFromJsonAsync<List<string>>(
                    $"api/TicketInterventionUsers/intervention/{InterventionId}/assigned-users");

                if (assigned == null || !assigned.Any())
                    return fallback;

                if (currentUserId != null && assigned.Contains(currentUserId))
                    return currentUserId;

                return assigned.FirstOrDefault(id => _users.Any(u => u.Id == id)) ?? fallback;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Assegnati dell'intervento non determinati: {ex.Message}");
                return fallback;
            }
        }

        private async Task<string> LoadBaseCurrencyAsync()
        {
            try
            {
                // Endpoint senza id: non presume che la riga delle impostazioni sia la numero 1.
                var settings = await Http.GetFromJsonAsync<CRM.Shared.GlobalSetting>("api/GlobalSettings");
                if (!string.IsNullOrWhiteSpace(settings?.BaseCurrency))
                    return settings.BaseCurrency.Trim().ToUpperInvariant();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Valuta base non determinata, si usa EUR: {ex.Message}");
            }

            return "EUR";
        }

        /// <summary>
        /// Conferma una delle valute compatibili col simbolo letto e chiede subito il cambio:
        /// scelta la valuta, il passo successivo e' sempre lo stesso.
        /// </summary>
        private async Task PickCurrency(string currency)
        {
            _model.Currency = currency;
            _currencyCandidates.Clear();
            await ProposeExchangeRate();
        }

        /// <summary>
        /// Chiede al server il cambio di riferimento per valuta e data. Resta una proposta:
        /// il campo e' modificabile perche' per un rimborso vale spesso il cambio della carta.
        /// </summary>
        private async Task ProposeExchangeRate()
        {
            _rateUnavailable = false;

            if (!_needsRate)
            {
                _model.ExchangeRate = null;
                return;
            }

            try
            {
                _loadingRate = true;
                StateHasChanged();

                var date = (_model.TransactionDate ?? DateTime.Today).ToString("yyyy-MM-dd");
                var currency = Uri.EscapeDataString(_model.Currency.Trim());

                var rate = await Http.GetFromJsonAsync<decimal?>($"api/ExpenseReceipts/exchange-rate?currency={currency}&date={date}");

                if (rate.HasValue && rate.Value > 0)
                    _model.ExchangeRate = rate;
                else
                    _rateUnavailable = true;
            }
            catch (Exception ex)
            {
                // Un cambio non disponibile non impedisce di salvare: la spesa resta da convertire.
                _rateUnavailable = true;
                Console.WriteLine($"Cambio non proposto: {ex.Message}");
            }
            finally
            {
                _loadingRate = false;
                StateHasChanged();
            }
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            _errorMessage = null;
            var file = e.File;

            if (file.Size > 10 * 1024 * 1024)
            {
                _errorMessage = "File troppo grande. Massimo 10 MB.";
                return;
            }

            byte[] fileBytes;
            try
            {
                using var originalStream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                using var memoryStream = new MemoryStream();
                await originalStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore lettura file: {ex.Message}";
                return;
            }

            var ct = file.ContentType ?? "application/octet-stream";
            _previewContentType = ct;
            _previewFileName = file.Name;
            _previewDataUrl = $"data:{ct};base64,{Convert.ToBase64String(fileBytes)}";

            await UploadAndProcessFile(fileBytes, file.Name, ct);
        }

        private async Task UploadAndProcessFile(byte[] fileBytes, string fileName, string contentType)
        {
            try
            {
                _isProcessing = true;
                StateHasChanged();

                // 1. Upload file as AttachmentFile
                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                content.Add(fileContent, "file", fileName);

                // Senza intervento il file non ha un contenitore a cui appendersi: si usa la
                // rotta che ne crea uno intestato a chi carica.
                var uploadUrl = _model.TicketInterventionId.HasValue
                    ? $"api/AttachmentFiles/{_model.TicketInterventionId}/upload"
                    : "api/AttachmentFiles/upload";

                var uploadResponse = await Http.PostAsync(uploadUrl, content);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    _errorMessage = "Errore durante il caricamento del file.";
                    _isProcessing = false;
                    return;
                }

                var uploadedFile = await uploadResponse.Content.ReadFromJsonAsync<AttachmentFileUploadResult>();
                _uploadedAttachmentFileId = uploadedFile.Id;

                // 2. Process with Azure Form Recognizer
                var processResponse = await Http.GetFromJsonAsync<ReceiptExtractionResult>(
                    $"api/ReceiptProcessor/process/{_uploadedAttachmentFileId}?kind={_kind}");

                _extractionResult = processResponse;

                if (_extractionResult.Success)
                {
                    // Popola il modello con i dati estratti
                    _model.AttachmentFileId = _uploadedAttachmentFileId;
                    _model.MerchantName = _extractionResult.MerchantName;
                    _model.TransactionDate = _extractionResult.TransactionDate;
                    _model.TotalAmount = _extractionResult.TotalAmount;
                    _model.TaxAmount = _extractionResult.TaxAmount;
                    // Valuta non riconosciuta = campo vuoto da compilare, non "EUR" per default:
                    // su uno scontrino estero l'assunzione produrrebbe un importo sbagliato.
                    _model.Currency = _extractionResult.Currency;

                    // Tipologia PROPOSTA: si presenta gia' scelta perche' nel caso normale e'
                    // giusta e confermarla non deve costare un clic in piu', ma resta marcata
                    // come proposta finche' non la si tocca o non si salva.
                    _model.Category = _extractionResult.SuggestedCategory;
                    _model.CategorySuggested = _extractionResult.SuggestedCategory;
                    _model.CategorySource = _extractionResult.CategorySource;
                    _model.CategoryConfidence = _extractionResult.CategoryConfidence;
                    _model.CategoryReason = _extractionResult.CategoryReason;

                    _currencyCandidates = _extractionResult.CurrencyCandidates ?? new List<string>();
                    _model.Description = $"{_extractionResult.MerchantName} - {_extractionResult.TransactionDate?.ToString("dd/MM/yyyy")}";
                    _model.ExtractionConfidence = _extractionResult.AverageConfidence;
                    _model.ExtractedFieldsJson = JsonSerializer.Serialize(_extractionResult);
                    _model.Documents = ToDocumentDtos(_extractionResult);

                    // Se lo scontrino e' in valuta estera il cambio serve subito: si propone
                    // senza aspettare che l'operatore esca dal campo valuta.
                    if (_needsRate)
                        await ProposeExchangeRate();
                }
                else
                {
                    // Estrazione fallita, ma salva comunque l'attachment
                    _model.AttachmentFileId = _uploadedAttachmentFileId;
                    _errorMessage = _extractionResult.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante l'elaborazione: {ex.Message}";
                Console.Error.WriteLine($"Errore upload/process: {ex}");
            }
            finally
            {
                _isProcessing = false;
                StateHasChanged();
            }
        }

        private void ShowManualForm()
        {
            _extractionResult = new ReceiptExtractionResult { Success = false };
            _previewDataUrl = null;
        }

        private async Task SaveReceipt()
        {
            try
            {
                // Vale ovunque, non solo sul costo generale: senza chi ha speso la spesa sparisce
                // dai totali per persona, ed e' il rimborso a non tornare.
                if (string.IsNullOrWhiteSpace(_model.IdUserSpender))
                {
                    _errorMessage = "Indica chi ha sostenuto la spesa: serve per il rimborso e per i totali per persona.";
                    return;
                }

                // Salvare senza valuta resta possibile - lo scontrino illeggibile esiste, e
                // costringere a inventarne una sarebbe peggio - ma deve essere una decisione presa,
                // non una svista. Il riquadro sopra il pulsante si puo' scorrere senza vederlo;
                // questa conferma no.
                if (!await ConfirmIncompleteAsync())
                    return;

                _isSaving = true;

                var isBatch = _model.Documents?.Count > 1;
                var response = await Http.PostAsJsonAsync(
                    isBatch ? "api/ExpenseReceipts/batch" : "api/ExpenseReceipts",
                    _model);

                if (response.IsSuccessStatusCode)
                {
                    if (isBatch)
                    {
                        NavigationManager.NavigateTo(ListUrlForSavedDate());
                        return;
                    }

                    var created = await response.Content.ReadFromJsonAsync<ExpenseReceiptDTO>();

                    // Si finisce sempre sul dettaglio di quello che si e' appena registrato, nel
                    // percorso da cui si e' arrivati quando ce n'e' uno.
                    if (created == null)
                        NavigationManager.NavigateTo(CancelUrl);
                    else if (created.TicketInterventionId != null)
                        NavigationManager.NavigateTo(
                            $"/TicketInterventions/{created.TicketInterventionId}/ExpenseReceipts/{created.Id}/Details");
                    else if (created.IdActivity != null)
                        NavigationManager.NavigateTo(
                            $"/Activities/{created.IdActivity}/ExpenseReceipts/{created.Id}/Details");
                    // Il percorso di entrata si conserva anche dopo il salvataggio: chi e' partito
                    // dalla fiera resta dentro la fiera, e il breadcrumb ce lo riporta.
                    else if (InitiativeContextId.HasValue)
                        NavigationManager.NavigateTo(
                            $"/Initiatives/{InitiativeContextId}/ExpenseReceipts/{created.Id}/Details");
                    else
                        NavigationManager.NavigateTo($"/ExpenseReceipts/{created.Id}/Details");
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _errorMessage = string.IsNullOrWhiteSpace(body)
                        ? "Errore durante il salvataggio della nota spese."
                        : body;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
                Console.Error.WriteLine($"Errore save receipt: {ex}");
            }
            finally
            {
                _isSaving = false;
            }
        }

        /// <summary>
        /// Chiede conferma quando la spesa nascerebbe gia' fuori dai totali. Restituisce true
        /// anche quando non c'e' niente da segnalare, cosi' il chiamante ha una condizione sola.
        /// </summary>
        /// <summary>
        /// Elenco su cui tornare dopo il salvataggio. Sull'elenco globale, che si apre sul mese
        /// corrente, una spesa di due mesi fa non comparirebbe: si chiede allora di aprirlo su
        /// tutto il periodo, altrimenti un salvataggio riuscito somiglia a uno fallito.
        /// </summary>
        private string ListUrlForSavedDate()
        {
            if (InterventionId.HasValue || ActivityId.HasValue || InitiativeContextId.HasValue)
                return CancelUrl;

            var date = EarliestSavedDate();
            var today = DateTime.Today;

            return date.HasValue && (date.Value.Year != today.Year || date.Value.Month != today.Month)
                ? "/ExpenseReceipts?period=all"
                : "/ExpenseReceipts";
        }

        private DateTime? EarliestSavedDate()
        {
            var dates = (_model.Documents ?? new List<ExpenseReceiptDocumentDTO>())
                .Where(d => d.TransactionDate.HasValue)
                .Select(d => d.TransactionDate.Value)
                .ToList();

            if (_model.TransactionDate.HasValue)
                dates.Add(_model.TransactionDate.Value);

            return dates.Count > 0 ? dates.Min() : null;
        }

        private async Task<bool> ConfirmIncompleteAsync()
        {
            var warnings = ConversionWarnings;
            if (warnings.Count == 0)
                return true;

            var message = string.Join(" ", warnings)
                + " Puoi salvare lo stesso e completare il dato più tardi, ma finché manca "
                + "questa spesa non compare in nessun totale.";

            var confirmed = await DialogService.Confirm(
                message,
                CurrencyMissing || DocumentsWithoutCurrency().Count > 0 ? "Valuta mancante" : "Cambio mancante",
                new Radzen.ConfirmOptions
                {
                    OkButtonText = "Salva così",
                    CancelButtonText = "Torna e completa"
                });

            return confirmed == true;
        }

        private void ResetForm()
        {
            // "Ricarica altro" serve a inserire piu' scontrini di fila: chi ha speso quasi sempre
            // resta lo stesso, quindi si conserva invece di farlo riselezionare ogni volta.
            var previousSpender = _model.IdUserSpender;

            // Stesso motivo dell'iniziativa: chi svuota la busta della trasferta carica dieci
            // scontrini di fila, tutti dello stesso viaggio. Rifarglielo scegliere ogni volta e'
            // il modo piu' sicuro per ritrovarsi meta' spese senza contesto.
            var previousInitiative = _model.IdInitiative;

            _model = new ExpenseReceiptCreateUpdateDTO
            {
                TicketInterventionId = InterventionId,
                IdActivity = ActivityId,
                IdInitiative = previousInitiative,
                IdUserSpender = previousSpender,
                Currency = _baseCurrency,
                TransactionDate = (_activity?.DoneDate ?? _activity?.DueDate)?.Date ?? DateTime.Today
            };
            _rateUnavailable = false;
            _currencyCandidates.Clear();
            _excludedDocuments.Clear();
            _extractionResult = null;
            _previewDataUrl = null;
            _uploadedAttachmentFileId = null;
            _errorMessage = null;
        }

        private string GetConfidenceClass(float confidence)
        {
            if (confidence >= 0.9f) return "bg-success";
            if (confidence >= 0.7f) return "bg-warning";
            return "bg-danger";
        }

        private static List<ExpenseReceiptDocumentDTO> ToDocumentDtos(ReceiptExtractionResult extraction)
        {
            var documents = extraction.DocumentResults?.Count > 0
                ? extraction.DocumentResults
                : new List<ReceiptExtractionResult> { extraction };

            return documents.Select((document, documentIndex) => new ExpenseReceiptDocumentDTO
            {
                SortOrder = documentIndex,
                PageFrom = document.PageFrom,
                PageTo = document.PageTo,
                DocumentType = document.DocumentType,
                MerchantName = document.MerchantName,
                Category = document.SuggestedCategory,
                CategorySource = document.CategorySource,
                CategoryConfidence = document.CategoryConfidence,
                CategoryReason = document.CategoryReason,
                TransactionDate = document.TransactionDate,

                // Se il singolo documento la valuta non la dichiara, vale quella riconosciuta sul
                // file: gli scontrini di uno stesso PDF vengono da una trasferta sola. Resta una
                // proposta modificabile riga per riga, non un'assunzione nascosta.
                Currency = string.IsNullOrWhiteSpace(document.Currency) ? extraction.Currency : document.Currency,
                SubtotalAmount = document.SubtotalAmount,
                TaxAmount = document.TaxAmount,
                TotalAmount = document.TotalAmount,
                ExtractionConfidence = document.AverageConfidence,
                Lines = (document.LineItems ?? new()).Select((line, lineIndex) => new ExpenseReceiptDocumentLineDTO
                {
                    SortOrder = lineIndex,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TotalPrice = line.TotalPrice,
                    ExtractionConfidence = line.Confidence
                }).ToList()
            }).ToList();
        }

        private static string FormatReceiptAmount(decimal? amount, string currency)
        {
            if (!amount.HasValue)
                return "-";

            var value = amount.Value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
            return string.IsNullOrWhiteSpace(currency) ? value : $"{value} {currency}";
        }

        private class AttachmentFileUploadResult
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
