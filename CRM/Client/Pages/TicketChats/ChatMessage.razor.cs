using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketChats
{
    public partial class ChatMessage: ComponentBase
    {
        [Inject] ITicketChatsService Service { get; set; }
        [Inject] IAttachmentsService AttachmentsService { get; set; }
        [Inject] IJSRuntime JSRuntime { get; set; }
        [Inject] DialogService DialogService { get; set; }
        [Inject] IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public TicketChatViewModel TicketChat { get; set; }

        [Parameter]
        public EventCallback<bool> Loaded { get; set; }

        [Parameter]
        public EventCallback OnDeleted { get; set; }

        private string _color = "black";

        private string _bgClass = "alert-success";

        private string _align = "ms-auto";

        protected override async Task OnInitializedAsync()
        {
            //if (TicketChat != null)
            //{
            //    if (TicketChat.TypeMessage == TypeMessage.Receved)
            //    {


            //        _color = TicketChat.Color;
            //        _bgClass = "alert-light";
            //        _align = "me-auto";
            //    }
            //    else if (TicketChat.TypeMessage == TypeMessage.Sended)
            //    {
            //        _color = "black";
            //        _bgClass = "alert-success";
            //        _align = "ms-auto";
            //    }
            //    else
            //    {
            //        _color = "black";
            //        _bgClass = "alert-warning";
            //        _align = "mse-auto";
            //    }

            //}
            await Service.ChatRead(TicketChat.Id, TicketChat);
            StateHasChanged();
            await base.OnInitializedAsync();
        }

        
        private bool _downloading;

        private bool _deleting;

        private async Task DeleteMessage()
        {
            if (_deleting || !TicketChat.CanDelete) return;

            var confirmed = await DialogService.Confirm(
                Localize["Eliminare questo messaggio? L'operazione non è reversibile."],
                Localize["Elimina messaggio"],
                new ConfirmOptions { OkButtonText = Localize["Elimina"], CancelButtonText = Localize["Annulla"] });

            if (confirmed != true) return;

            _deleting = true;
            StateHasChanged();
            try
            {
                if (await Service.Delete(TicketChat.Id))
                {
                    TicketChat.Deleted = true;
                    TicketChat.CanDelete = false;
                    TicketChat.Message = null;
                    TicketChat.AttachmentFileId = null;
                    TicketChat.AttachmentId = null;

                    if (OnDeleted.HasDelegate)
                        await OnDeleted.InvokeAsync();
                }
            }
            finally
            {
                _deleting = false;
                StateHasChanged();
            }
        }

        private async Task DownloadFile()
        {
            if (TicketChat.AttachmentId == null || _downloading) return;
            _downloading = true;
            StateHasChanged();
            try
            {
                // Stesso percorso usato (e funzionante) nella pagina Allegati.
                var (bytes, contentType, fileName) = await AttachmentsService.DownloadFiles(TicketChat.AttachmentId.Value);
                if (bytes.Length > 0)
                {
                    await JSRuntime.InvokeVoidAsync("downloadFromByteArray", new
                    {
                        ByteArray = bytes,
                        ContentType = contentType,
                        FileName = fileName
                    });
                }
            }
            finally
            {
                _downloading = false;
                StateHasChanged();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            

            if (firstRender)
            {
               
                if (Loaded.HasDelegate)
                    await Loaded.InvokeAsync(TicketChat.Last);
            }
            await base.OnAfterRenderAsync(firstRender);
        }
    }

    
}
