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
using CRM.Shared.DTOs;

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
        IAttachmentsService Service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IUserService UserService { get; set; }

        [Inject]
        IFoldersService FoldersService { get; set; }

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

        private RadzenDataGrid<AttachmentDTO> _grdAttachment;

        private PagingResponse<AttachmentDTO> _attachments = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private AttachmentDTO _attachment;

        private bool _isLoading = false;

        private int _pageSize = 10;

        private bool _saving = false;

        private string _filterString = null;

        private string _userFilter = null;

        private int? _folderId = null;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private List<FolderDTO> _folders = new List<FolderDTO>();

        private AttachmentsFilter _filter = new AttachmentsFilter() { PageSize = 10, Skip = 0, Top = 10 };

        protected override async Task OnInitializedAsync()
        {
            

            
            await LoadUser();
            await LoadFolders();
            await LoadData();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            
            _isLoading = true;
            _filter.IdParant = IdParent;
            _filter.AttchmentType = AttachmentType;
            _filter.FolderId = _folderId;
            if (args != null)
            {
                _filter.Skip = args.Skip;
                _filter.Top = args.Top;
                _filter.OrderBy = args.OrderBy;
                if (args.Filters != null && args.Filters.Any())
                {
                    if (_filter.Filter?.Length > 0)
                        _filter.Filter += " And ";
                    _filter.Filter += args.Filter;
                }
            }
            var resp = await Service.GetPagingAsync(_filter);
            if (resp != null)
            {
                _attachments = resp;
                _paging = _attachments.MetaData;
            }

            
            _isLoading = false;

        }

        private async Task LoadFolders()
        {
            if (FoldersService != null)
            {
                _folders = await FoldersService.GetListAsync(new FolderFilter() { AttachmentType = AttachmentType });
                StateHasChanged();
            }
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
                    NavigationManager.NavigateTo($"/DocViewer/{id}");
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
                var resp = await Service.DeleteAsync(id); 

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
            //var response = await HttpClient.GetAsync($"{ConstHelper.AttachmentsPath}/download/{id}");

            //if (response.IsSuccessStatusCode)
            //{
            //    var bytes = await response.Content.ReadAsByteArrayAsync();


            //    AttachmentResponse header = JsonSerializer.Deserialize<AttachmentResponse>(response.Headers
            //            .GetValues(ConstHelper.FileHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
               

            //    await jSRuntime.InvokeVoidAsync(
            //      "downloadFromByteArray",
            //      new
            //      {
            //          ByteArray = bytes,
            //          FileName = header.Name,
            //          ContentType = header.ContentType
            //      });
            //}

            var resp = await Service.DownloadFiles(id);
            if (resp.Bytes.Length > 0)
            {
                await jSRuntime.InvokeVoidAsync(
                  "downloadFromByteArray",
                  new
                  {
                      ByteArray = resp.Bytes,
                      FileName = resp.FileName,
                      ContentType = resp.ContentType
                  });
            }
        }

        private async Task OnChangeFolder()
        {
            await LoadData();

            
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
