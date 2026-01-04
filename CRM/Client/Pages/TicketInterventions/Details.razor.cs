using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Radzen;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        
        [Inject]
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }

        [Inject]
        private HttpClient _httpClient { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickPdfViewer { get; set; }

        public Action OnClosePdfViewer { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        protected enum MsgBoxComand
        {
            ViewReport,
            CreateReport,
            DownloadReport
        }

        
        private TicketIntervention _ticketIntervention = null;

        private string _typeMessage;
        private string _message = null;

        private string _msgBox;

        private MsgBoxComand _msgBoxComand;

        private bool _creatingReport = false;
        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        protected async Task LoadData()
        {
            try
            {
                _message = null;

                if (Id != null)
                {

                    _ticketIntervention = await _service.Get(Id.Value);
                }
                else
                    _ticketIntervention = new TicketIntervention();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/TicketsIntervention/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/TicketsIntervention/Index");
        }

        protected void SendInvitation()
        {

        }

        protected async Task ReportCreate()
        {
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgWaitingBox");
            
            _creatingReport = true;
            StateHasChanged();
            var resp = await _httpClient.GetFromJsonAsync<bool>($"{ConstHelper.TicketsInterventionsPath}/Report/{_ticketIntervention.Id}");

            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgWaitingBox");

            if (resp)
            {
                _typeMessage = "alert-success";
                _message = "Report Creato con successo";

                ReportView();

            }
            else
            {
                _typeMessage = "alert-danger";
                _message = "Report NON creato: Errore durante la sua creazione";

                await LoadData();
                
            }
            _creatingReport = false;
            StateHasChanged();
        }

        protected void ReportView()
        {
            if (OnClickPdfViewer != null)
                OnClickPdfViewer();
            else
                NavigationManager.NavigateTo($"/TicketInterventions/PDFLoading/{_ticketIntervention.Id}");
        }

        protected async Task CreateReportPrepare()
        {


            if (_ticketIntervention == null)
                return;

            if (_ticketIntervention.AttachmentExist)
                _msgBox = Localize["AttentionReportAlreadyPresent"];
            else
                _msgBox = Localize["Creare il Report di intervento?"];

            if (await DialogService.Confirm(_msgBox) == true)
            {

                await ReportCreate();
            }
            

        }

        

     

        protected async Task DownloadReport()
        {

            if (await DialogService.Confirm(Localize["Download Report?"]) == true)
            {
                var response = await _httpClient.GetAsync($"{ConstHelper.TicketsInterventionsPath}/Download/{Id}");

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    AttachmentResponse header = JsonSerializer.Deserialize<AttachmentResponse>(response.Headers
                            .GetValues(ConstHelper.FileHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

                    await JSRuntime.InvokeVoidAsync(
                      "downloadFromByteArray",
                      new
                      {
                          ByteArray = bytes,
                          FileName = header.Name,
                          ContentType = header.ContentType
                      });
                }
            }
        }
        protected async void ReportUploadDialog()
        {
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgUploadFile");
        }

        private async Task<bool> UploadReport(UploadFilesModel file)
        {
            var resp = await _httpClient.PostAsJsonAsync<UploadFilesModel>($"{ConstHelper.TicketsInterventionsPath}/UploadReport/{Id}", file);

            return resp.StatusCode == System.Net.HttpStatusCode.OK;
        }

        private async Task OnUploaded(bool state)
        {
            if (state)
            {
                await JSRuntime.InvokeVoidAsync("CloseModal", "dlgUploadFile");
                StateHasChanged();
                

            }
        }
        private async void CloseUploadReport()
        {
            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgUploadFile");
        }

        private void CloseReportView()
        {

        }
    }
}
