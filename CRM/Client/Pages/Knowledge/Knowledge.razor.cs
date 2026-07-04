using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Knowledge
{
    public partial class Knowledge : ComponentBase
    {
        [Inject] IKnowledgeApiService Service { get; set; }

        [Inject] IProductsService ProductsService { get; set; }

        [Inject] DialogService DialogService { get; set; }

        [Inject] NotificationService Notify { get; set; }

        private List<ProductKnowledgeDTO> _items = new();

        private List<ProductDTO> _products = new();

        private bool _loading;

        private bool _editing;

        private string _search = string.Empty;

        private ProductKnowledge _current = new();

        // Import documento
        private IBrowserFile? _uploadFile;

        private int? _importProductId;

        private string _importCategory = string.Empty;

        private bool _importing;

        protected override async Task OnInitializedAsync()
        {
            _products = await ProductsService.GetListAsync(new ProductFilter()) ?? new();
            await LoadItems();
        }

        private async Task LoadItems()
        {
            _loading = true;
            StateHasChanged();

            _items = await Service.GetList(new ProductKnowledgeFilter
            {
                Search = string.IsNullOrWhiteSpace(_search) ? null : _search
            });

            _loading = false;
            StateHasChanged();
        }

        private void NewItem()
        {
            _current = new ProductKnowledge();
            _editing = true;
        }

        private async Task EditItem(int id)
        {
            var dto = await Service.Get(id);
            if (dto == null)
                return;

            _current = new ProductKnowledge
            {
                Id = dto.Id,
                IdProduct = dto.IdProduct,
                Title = dto.Title,
                Content = dto.Content,
                Category = dto.Category
            };
            _editing = true;
        }

        private async Task SaveItem()
        {
            if (string.IsNullOrWhiteSpace(_current.Title) || string.IsNullOrWhiteSpace(_current.Content))
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Warning,
                    Summary = "Dati mancanti",
                    Detail = "Titolo e contenuto sono obbligatori.",
                    Duration = 4000
                });
                return;
            }

            var saved = await Service.Save(_current);
            if (saved != null)
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Salvato",
                    Detail = "Voce di conoscenza salvata.",
                    Duration = 3000
                });
                _editing = false;
                await LoadItems();
            }
            else
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Errore",
                    Detail = "Salvataggio non riuscito.",
                    Duration = 4000
                });
            }
        }

        private void CancelEdit()
        {
            _editing = false;
        }

        private async Task DeleteItem(ProductKnowledgeDTO item)
        {
            var confirm = await DialogService.Confirm(
                $"Eliminare la voce \"{item.Title}\"?", "Conferma eliminazione",
                new ConfirmOptions { OkButtonText = "Elimina", CancelButtonText = "Annulla" });

            if (confirm != true)
                return;

            if (await Service.Delete(item.Id))
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Eliminata",
                    Duration = 3000
                });
                await LoadItems();
            }
            else
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Errore",
                    Detail = "Eliminazione non riuscita.",
                    Duration = 4000
                });
            }
        }

        private async Task GenerateEmbeddings()
        {
            var stats = await Service.GenerateEmbeddings(50);

            Notify.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Embedding",
                Detail = stats == null
                    ? "Operazione non riuscita."
                    : $"Generati: {stats.Processed} · Mancanti: {stats.Remaining} · Totale: {stats.Total}",
                Duration = 5000
            });

            await LoadItems();
        }

        private void OnFileSelected(InputFileChangeEventArgs e)
        {
            _uploadFile = e.File;
        }

        private async Task ImportDocumentAsync()
        {
            if (_uploadFile == null)
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Warning,
                    Summary = "Nessun file",
                    Detail = "Seleziona un documento (PDF, DOCX, TXT, MD).",
                    Duration = 4000
                });
                return;
            }

            _importing = true;
            StateHasChanged();

            try
            {
                // Bufferizza il file lato client, poi caricalo
                using var browserStream = _uploadFile.OpenReadStream(50 * 1024 * 1024);
                using var ms = new MemoryStream();
                await browserStream.CopyToAsync(ms);
                ms.Position = 0;

                var res = await Service.ImportDocument(
                    ms, _uploadFile.Name, _importProductId,
                    string.IsNullOrWhiteSpace(_importCategory) ? null : _importCategory);

                if (res != null)
                {
                    Notify.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Success,
                        Summary = "Import completato",
                        Detail = $"Blocchi creati: {res.Chunks}." + (string.IsNullOrEmpty(res.Message) ? "" : $" {res.Message}"),
                        Duration = 6000
                    });

                    _uploadFile = null;
                    _importProductId = null;
                    _importCategory = string.Empty;
                    await LoadItems();
                }
                else
                {
                    Notify.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = "Import non riuscito",
                        Detail = "Verifica il formato del file e riprova.",
                        Duration = 5000
                    });
                }
            }
            catch (Exception ex)
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Errore import",
                    Detail = ex.Message,
                    Duration = 6000
                });
            }
            finally
            {
                _importing = false;
                StateHasChanged();
            }
        }
    }
}
