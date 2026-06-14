using CRM.Client.Helpers;
using CRM.Client.Shared.Components;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using Microsoft.AspNetCore.WebUtilities;

namespace CRM.Client.Pages.DocViewer
{
    public partial class Viewer : ComponentBase, IAsyncDisposable
    {
        [Inject] 
        private IJSRuntime JS { get; set; } = default!;
        
        [Inject] 
        private HttpClient Http { get; set; } = default!;

        [Inject] 
        private DialogService DialogService { get; set; } = default!;

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]    
        private NavigationManager NavigationManager { get; set; } = default!;

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public bool DownloadEnabled { get; set; } = true;

        [Parameter]
        public bool CloseEnabled { get; set; } = true;

        [Parameter]
        public bool Embedded { get; set; } = false;

        private bool _loaded;
        private string _loadingMessage = "Caricamento documento...";
        private readonly string _canvasId = $"dxfCanvas_{Guid.NewGuid():N}";
        private readonly string _fileHostId = $"fileHost_{Guid.NewGuid():N}";
        private string RootStyle => Embedded
            ? "position:relative; width:100%; height:100%; min-height:260px; margin:0; padding:0; box-sizing:border-box;"
            : "position:absolute; inset:0; margin:0; padding:0; box-sizing:border-box;";

        private ElementReference containerRef;
        private ElementReference canvasRef;
        private ElementReference fileHostRef;
        private bool _initialized;
        private string _currentContentType;
        private string _currentFileName;

        protected override async Task OnInitializedAsync()
        {
            Console.WriteLine($"Viewer initialized with Id: {Id}");
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
               
                _initialized = true;
               
                // Inizializza il ridimensionamento del canvas lato JS
                await JS.InvokeVoidAsync("dialogSizing.setCanvasToContainer", containerRef, canvasRef);
              
                await LoadData();
                StateHasChanged();
            }
        }


        private async Task LoadData()
        {
            try
            {
                _loadingMessage = "Caricamento documento...";
                StateHasChanged();

                var url = $"api/attachments/files/{Id}";

                using var response = await Http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType
                                  ?? "application/octet-stream";

                _currentContentType = contentType;
                _currentFileName = $"file_{Id}";

                // Formati Office/OpenDocument che richiedono conversione server-side PDF
                var needsConversion = new[]
                {
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // DOCX
                    "application/msword", // DOC
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // XLSX
                    "application/vnd.ms-excel", // XLS
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation", // PPTX
                    "application/vnd.ms-powerpoint", // PPT
                    "application/vnd.oasis.opendocument.text", // ODT
                    "application/vnd.oasis.opendocument.spreadsheet", // ODS
                    "application/vnd.oasis.opendocument.presentation" // ODP
                };

                // Se è un file Office/OpenDocument, usa endpoint di conversione PDF
                if (needsConversion.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                {
                    _loadingMessage = "Conversione documento in PDF...";
                    StateHasChanged();

                    // Chiama endpoint che converte in PDF
                    var pdfUrl = $"api/attachments/files/{Id}/pdf";
                    using var pdfResponse = await Http.GetAsync(pdfUrl);
                    pdfResponse.EnsureSuccessStatusCode();

                    var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();

                    _loadingMessage = "Rendering PDF...";
                    StateHasChanged();

                    // Update for PDF export
                    _currentContentType = "application/pdf";
                    _currentFileName = $"file_{Id}.pdf";

                    // Mostra il PDF inline
                    await JS.InvokeVoidAsync("displayFileInElement", fileHostRef, "application/pdf", pdfBytes, _currentFileName);
                    await SetViewerMode(false);
                }
                else
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        _loadingMessage = "Rendering PDF...";
                        StateHasChanged();

                        _currentFileName = $"file_{Id}.pdf";

                        // PDF: mostra nel fileHost, nascondi canvas
                        await JS.InvokeVoidAsync("displayFileInElement", fileHostRef, contentType, bytes, _currentFileName);
                        await SetViewerMode(false);
                    }
                    else if (contentType.Contains("dxf", StringComparison.OrdinalIgnoreCase) ||
                             contentType.Contains("autocad", StringComparison.OrdinalIgnoreCase))
                    {
                        _loadingMessage = "Rendering DXF...";
                        StateHasChanged();

                        _currentFileName = $"file_{Id}.dxf";

                        // DXF: mostra canvas, nascondi fileHost
                        await SetViewerMode(true);
                        await JS.InvokeVoidAsync("loadDxfFromBytes", _canvasId, bytes);
                        await JS.InvokeVoidAsync("dialogSizing.setCanvasToContainer", containerRef, canvasRef);
                    }
                    else
                    {
                        _loadingMessage = "Rendering file...";
                        StateHasChanged();

                        _currentFileName = $"file_{Id}";

                        // Altri tipi: prova a mostrare nel fileHost (immagini, ecc.)
                        await JS.InvokeVoidAsync("displayFileInElement", fileHostRef, contentType, bytes, _currentFileName);
                        await SetViewerMode(false);
                    }
                }

                _loaded = true;
            }
            catch (JSException jsEx)
            {
                Console.Error.WriteLine("Errore JS interop: " + jsEx);
                _loadingMessage = "Errore durante il caricamento";
                _loaded = true; // nasconde spinner anche in caso di errore
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Errore generico in LoadData: " + ex);
                _loadingMessage = "Errore: " + ex.Message;
                _loaded = true;
            }
        }
        private async Task Export()
        {
            try
            {
                // Se è un DXF, esporta come immagine dal canvas
                if (_currentContentType?.Contains("dxf", StringComparison.OrdinalIgnoreCase) == true ||
                    _currentContentType?.Contains("autocad", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await JS.InvokeVoidAsync(
                        "exportDxfImageHighRes",
                        _canvasId,
                        "componente.png"
                    );
                }
                else
                {
                    // Per PDF, Office e altri file, scarica il file originale
                    var url = $"api/attachments/files/{Id}";
                    using var response = await Http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var contentDisposition = response.Content.Headers.ContentDisposition;
                    var fileName = contentDisposition?.FileName?.Trim('\"') ?? _currentFileName;

                    await JS.InvokeVoidAsync(
                        "downloadFromByteArray",
                        new
                        {
                            ByteArray = bytes,
                            FileName = fileName,
                            ContentType = _currentContentType
                        });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore durante l'export: {ex.Message}");
                await ShowAlert("Errore durante il download del file.", "Errore");
            }
        }
        

        protected async Task Close()
        {
            try
            {
                // Se aperto come SideDialog / Dialog Radzen, questo chiude la finestra
                await DialogService?.CloseSideAsync();
            }
            catch (Exception)
            {
                // Fallback: se non è una dialog, torna alla lista degli allegati
                try
                {
                    NavigationManager.NavigateTo("/attachments");
                }
                catch { /* swallow */ }
            }
        }


        // Sostituisci il metodo ShowAlert con la versione corretta che restituisce Task
        private Task ShowAlert(string message, string title)
        {
            var parameters = new Dictionary<string, object>()
            {
                { "Message", message },
                { "Title", title }
            };
            var options = new DialogOptions() { Width = "400px", Height = "200px", Resizable = false, Draggable = false };
            return DialogService.OpenAsync<Alert>(title, parameters, options);
        }

        private async Task SetViewerMode(bool showDxfCanvas)
        {
            var script = showDxfCanvas
                ? $"document.getElementById('{_canvasId}').style.display='block'; document.getElementById('{_fileHostId}').style.display='none';"
                : $"document.getElementById('{_canvasId}').style.display='none'; document.getElementById('{_fileHostId}').style.display='block';";
            await JS.InvokeVoidAsync("eval", script);
        }

        // aggiungi scollegamento observer nel DisposeAsync
        public async ValueTask DisposeAsync()
        {
            try
            {
                try
                {
                    await JS.InvokeVoidAsync("dialogSizing.disconnectObserver", containerRef);
                    await JS.InvokeVoidAsync("cleanupDxfViewer", _canvasId);
                    await JS.InvokeVoidAsync("cleanupFileHost", fileHostRef);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Errore disconnect observer JS: " + ex);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Errore DisposeAsync: " + ex);
            }
        }
    }
}
