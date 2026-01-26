using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazoringComponents;
using Radzen;
using Microsoft.Extensions.Localization;
using Radzen.Blazor;

namespace CRM.Client.Pages.Attachments
{
    [Authorize]
    public partial class Index: ComponentBase
    {

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IJSRuntime jSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IUserService UserService { get; set; }


        [Parameter]
        public int? IdParent { get; set; }

        [Parameter]
        public AttachmentTypes AttachmentType { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public bool ReadOnly { get; set; } = false;

        private RadzenDataGrid<Attachment> _grdAttachment;

        private PagingResponse<Attachment> _attachments = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private Attachment _attachment;

        private bool _isLoading = false;

        private int _pageSize = 10;

        private bool _saving = false;

        private string _filter = null;

        private string _userFilter = null;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        protected override async Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif


            await LoadData();
            await LoadUser();

        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            
            _isLoading = true;
            if (HttpClient != null)
            {
                AttachmentsFilter paging = new AttachmentsFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;

                paging.IdParant = IdParent;
                paging.AttchmentType = AttachmentType;


                if (args != null)
                {
                    paging.Skip = args.Skip;
                    paging.Top = args.Top;
                    paging.OrderBy = args.OrderBy;

                    

                    if (args.Filters != null && args.Filters.Any())
                    {
                        if (paging.Filter?.Length > 0)
                            paging.Filter += " And ";
                        paging.Filter += args.Filter;
                    }
                }

                _attachments = await RestClientHelper.Get<Attachment>(HttpClient, ConstHelper.AttachmentsPath, paging);

                if (_attachments == null)
                {
                    _attachments = new PagingResponse<Attachment>();
                    _attachments.Items = new List<Attachment>();
                    _attachments.MetaData = new PagingHeaderModel();
                }

                _paging.TotalCount = _attachments.MetaData.TotalCount;
            }
            _isLoading = false;

        }

      

        protected async Task SearchSubmit()
        {
            if (_filter != null)
                await LoadData();
        }

        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
            {
                if (AttachmentType == AttachmentTypes.DXF)
                    NavigationManager.NavigateTo($"/DxfViewer/{id}");
                else
                    NavigationManager.NavigateTo($"/Attachments/{id}/Details");
            }
        }

        protected void Edit(int? id)
        {
            if (OnClickEdit != null)
            {
                OnClickEdit(id);
            }
            else
            {
                NavigationManager.NavigateTo($"/Attachments/{id}/Edit");
            }
        }

        protected void NewAttachment()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo("/Attachments/New");
        }

        protected async Task Delete(int id)
        {
            if (await DialogService.Confirm("Eliminare il documento selezionato?", "Attenzione") == true)
            {
                var resp = await HttpClient.DeleteAsync($"{ConstHelper.AttachmentsPath}/{id}");

                await LoadData();
            }
        }

        protected async Task Download(int id)
        {

            if (await DialogService.Confirm("Download dell'allegato", "Download") == true)
            {
                await OnClickDownloadButton(id);
            }
        }

      

        private async Task OnClickDownloadButton(int id)
        {
            // Please imagine the situation that the API is protected by
            // token-based authorization (non cookie-based authorization).
           // var bytes = await HttpClient.GetByteArrayAsync($"{ConstHelper.AttachmentsPath}/download/{item.Id}");
            var response = await HttpClient.GetAsync($"{ConstHelper.AttachmentsPath}/download/{id}");

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();


                AttachmentResponse header = JsonSerializer.Deserialize<AttachmentResponse>(response.Headers
                        .GetValues(ConstHelper.FileHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
               

                await jSRuntime.InvokeVoidAsync(
                  "downloadFromByteArray",
                  new
                  {
                      ByteArray = bytes,
                      FileName = header.Name,
                      ContentType = header.ContentType
                  });
            }
        }

        private async Task<string> GetUser(string id)
        {
            var user = await UserService.GetItem<ApplicationUser, string>(id, ConstHelper.UsersPath);

            if (user != null)
            {
                return user.NameComplete;
            }
            else
                return "";
        }


        private async Task LoadUser()
        {
            if (UserService != null)
            {
                _users = await UserService.Get<ApplicationUser>(ConstHelper.UsersPath);

                StateHasChanged();
            }
        }

        async Task OnFilterUserChanged(object value)
        {
          
           await _grdAttachment.FirstPage();
        }

        private async void OnCloseFilter()
        {
            //  await JSRuntime.InvokeVoidAsync("Radzen.closePopup", $"popup{grdInterventions.UniqueID}SupportType");
            //  StateHasChanged();

            await _grdAttachment.FirstPage();
        }
    }
}
