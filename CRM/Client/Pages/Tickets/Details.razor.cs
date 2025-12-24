using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private ITicketService _service { get; set; }

       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public object IdTicket { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickTicketClose { get; set; }

        [Parameter]
        public EventCallback OnClickPrint { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        [Parameter]
        public bool HeaderVisible { get; set; } = false;

        [Parameter]
        public string BackUrl { get; set; }


        private bool _panelAssign = false;
        private bool _isDownloadingPdf = false;

        private TicketModel _ticket = null;


       


        protected override async Task OnInitializedAsync()
        {
            if (Id == null && IdTicket != null && int.TryParse(IdTicket.ToString(), out int id))
            {
                Id = id;
            }
       
            await LoadData();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (IdTicket != null && int.TryParse(IdTicket.ToString(), out int id))
            {
                Id = id;
                await LoadData();
            }
        }
        private async Task LoadData()
        {
            try
            {

                if (Id != null)
                {

                    _ticket = await _service.GetDetails(Id.Value);
                }
                else
                    _ticket = new TicketModel();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            finally
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Tickets/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                BackToUrl();
            //NavigationManager.NavigateTo("/Tickets/Index");
        }

        protected void TicketClose()
        {
            if (OnClickTicketClose != null)
                OnClickTicketClose();
            else
                NavigationManager.NavigateTo($"/Tickets/Close/{Id}");
        }

        private async void TicketPrint()
        {
            if (OnClickPrint.HasDelegate)
                await OnClickPrint.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/Tickets/Report/{Id}");
        }

        /// <summary>
        /// Scarica il PDF del ticket con QuestPDF
        /// </summary>
        private async Task DownloadPdf()
        {
            try
            {
                if (Id == null || Id <= 0)
                    return;

                _isDownloadingPdf = true;
                StateHasChanged();

                // Chiamata API per ottenere il PDF
                var response = await HttpClient.GetAsync($"api/Tickets/pdf/{Id}");

                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = $"Ticket_{Id}_{DateTime.Now:yyyyMMdd}.pdf";

                    // Scarica il file nel browser
                    await JSRuntime.InvokeVoidAsync("downloadFileFromBytes", 
                        fileName, 
                        "application/pdf", 
                        fileBytes);
                }
                else
                {
                    // Mostra errore nella console (o usa un toast/notification service se disponibile)
                    Console.WriteLine($"Errore download PDF: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore download PDF: {ex.Message}");
            }
            finally
            {
                _isDownloadingPdf = false;
                StateHasChanged();
            }
        }

        private void PrepareAssign()
        {
            _panelAssign = true;
            StateHasChanged();
        }

        private async Task OnAssignClose()
        {
            _panelAssign = false;
            await LoadData();
            StateHasChanged();
        }
        protected void SendInvitation()
        {

        }

        private void BackToUrl()
        {
            if (BackUrl == null || BackUrl.Length == 0)
            {
                BackUrl = "/Tickets/Index";
            }
            else
                BackUrl = BackUrl.Replace("-", "/");

            NavigationManager.NavigateTo($"/Tickets/Index/{BackUrl}");
        }


    }
}
