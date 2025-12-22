using CRM.Client.Helpers;
using CRM.Client.Shared.Components;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Syncfusion.Blazor.Navigations;
using Syncfusion.Blazor.PdfViewer;
using Syncfusion.Blazor.SfPdfViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class PDFLoading: ComponentBase
    {
        [Inject]
        HttpClient _httpClient { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnUpdatePdfViewer { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }


            
        private string _msgBox;

        protected SfPdfViewer2 _pdfViewer;

        private string _document = null;

        protected override async Task OnInitializedAsync()
        {
            await GetReport();
            await base.OnInitializedAsync();
        }
        private void Select(MenuEventArgs<MenuItem> args)
        {
            // Your code here. 
        }

        protected async void ReportUpload()
        {
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgUploadFile");
        }

       
        protected async void ReportEmail()
        {
            
            await GetReport();

        }

        protected async Task ReportCreatePrepare()
        {
            _msgBox = "Ricreare il Report?";
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgMsgBox");
        }
        protected async void ReportCreate()
        {

            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgMsgBox");
            StateHasChanged();
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgWaitingBox");
            
            var resp = await _httpClient.GetFromJsonAsync<bool>($"{ConstHelper.TicketsInterventionsPath}/Report/{Id}");
           
            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgWaitingBox");

            OnUpdatePdfViewer?.Invoke();
        }

        private async Task<bool> UploadReport(UploadFilesModel file)
        {
            var resp = await _httpClient.PostAsJsonAsync<UploadFilesModel>($"{ConstHelper.TicketsInterventionsPath}/UploadReport/{Id}", file);

            return resp.StatusCode == System.Net.HttpStatusCode.OK;
        }

        private async void CloseUploadReport()
        {
            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgUploadFile");
        }

        private async void OnUploaded(bool state)
        {
            if (state)
            {
                await JSRuntime.InvokeVoidAsync("CloseModal", "dlgUploadFile");
                OnUpdatePdfViewer?.Invoke();
               
            }
        }

        private async Task GetReport()
        {
            _document = await _httpClient.GetStringAsync($"{ConstHelper.TicketsInterventionsPath}/getreport/{Id}");

            StateHasChanged();
        }

        private async Task DownloadReport()
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

        private async Task OpenSendEmail()
        {
            await DialogService.OpenAsync<EmailSender>("Invio Report", new Dictionary<string, object>() { { "SendEmailApiUrl", $"{ConstHelper.TicketsInterventionsPath}/Email/{Id}" },
                {"EmailAddressesApiUrl", $"{ConstHelper.TicketsInterventionsPath}/CompanyEmailAdresses/{Id}" } }, new DialogOptions() { Height="auto", Width="50%"  });
        }

        private async Task OpenEmailSent()
        {
            await DialogService.OpenAsync<CRM.Client.Pages.EmailsSent.Info>(Localize["Email Inviate"], null, new DialogOptions() { Height = "auto", Style="min-height='400px"});
        }
        protected void Close()
        {
            OnClickDetails?.Invoke(Id);
        }
    }
}
