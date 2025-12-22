using CRM.Client.Helpers;
using CRM.Client.Pages.ProductParameters;
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

namespace CRM.Client.Pages.DxfViewer
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

        private bool _loaded;
        private string _loadingMessage = "Caricamento documento...";

        private ElementReference containerRef;
        private ElementReference canvasRef;
        private ElementReference fileHostRef;
        private bool _initialized;

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

                    // Mostra il PDF inline
                    await JS.InvokeVoidAsync("displayFileInElement", fileHostRef, "application/pdf", pdfBytes, $"file_{Id}.pdf");
                    await JS.InvokeVoidAsync("eval", "document.getElementById('dxfCanvas').style.display='none'; document.getElementById('fileHost').style.display='block';");
                }
                else
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        _loadingMessage = "Rendering PDF...";
                        StateHasChanged();

                        // PDF: mostra nel fileHost, nascondi canvas
                        await JS.InvokeVoidAsync("displayFileInElement", fileHostRef, contentType, bytes, $"file_{Id}.pdf");
                        await JS.InvokeVoidAsync("eval", "document.getElementById('dxfCanvas').style.display='none'; document.getElementById('fileHost').style.display='block';");
                    }
                    else if (contentType.Contains("dxf", StringComparison.OrdinalIgnoreCase) ||
                             contentType.Contains("autocad", StringComparison.OrdinalIgnoreCase))
                    {
                        _loadingMessage = "Rendering DXF...";
                        StateHasChanged();

                        // DXF: mostra canvas, nascondi fileHost
                        await JS.InvokeVoidAsync("eval", "document.getElementById('dxfCanvas').style.display='block'; document.getElementById('fileHost').style.display='none';");
                        await JS.InvokeVoidAsync("loadDxfFromBytes", "dxfCanvas", bytes);
                        await JS.InvokeVoidAsync("dialogSizing.setCanvasToContainer", containerRef, canvasRef);
                    }
                    else
                    {
                        _loadingMessage = "Rendering file...";
                        StateHasChanged();

                        // Altri tipi: prova a mostrare nel fileHost (immagini, ecc.)
                        await JS.InvokeVoidAsync("displayFileInElement", fileHostRef, contentType, bytes, $"file_{Id}");
                        await JS.InvokeVoidAsync("eval", "document.getElementById('dxfCanvas').style.display='none'; document.getElementById('fileHost').style.display='block';");
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
            await JS.InvokeVoidAsync(
                "exportDxfImageHighRes",
                "dxfCanvas",
                "componente.png"
            );
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
        // aggiungi scollegamento observer nel DisposeAsync
        public async ValueTask DisposeAsync()
        {
            try
            {
                try
                {
                    await JS.InvokeVoidAsync("dialogSizing.disconnectObserver", containerRef);
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