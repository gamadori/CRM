using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Folders
{
    public partial class Index : ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IFoldersService Service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        private FolderFilter _filter = new FolderFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<Folder> _folderGrid;

        private List<Folder> _folders;

        private Folder _folder;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            await GetFolders();
            await base.OnInitializedAsync();
        }

        private async Task GetFolders()
        {
            try
            {
                _isLoading = true;

                PagingResponse<FolderDTO> pagingResponse = await Service.GetPagingAsync(_filter);

                if (pagingResponse != null)
                {
                    _folders = pagingResponse.Items.Select(i => new Folder
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Description = i.Description
                    }).ToList();
                    _paging = pagingResponse.MetaData;
                }
                else
                    Notify("Error", NotificationSeverity.Error);
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {
                Notify(ex.Message, NotificationSeverity.Error);
            }
            catch (Exception ex)
            {
                Notify(ex.Message, NotificationSeverity.Error);
            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        async Task EditRow(Folder folder)
        {
            await _folderGrid.EditRow(folder);
        }

        async Task OnUpdateRow(Folder item)
        {
            if (item == _folder)
            {
                _folder = null;
            }
            var resp = await Service.PostAsync(item);

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);
            }
            else
                Notify(Localize["UpdatedData"], NotificationSeverity.Success);
        }

        private async Task SaveRow(Folder item)
        {
            if (item == _folder)
            {
                _folder = null;
            }

            await _folderGrid.UpdateRow(item);
            await GetFolders();
        }

        private async Task CancelEdit(Folder item)
        {
            if (item == _folder)
            {
                _folder = null;
            }

            _folderGrid.CancelEditRow(item);
            await Service.PostAsync(item);
        }

        async Task DeleteRow(Folder item)
        {
            if (await DialogService.Confirm(Localize["DeleteFolder?"], Localize["Delete"]))
            {
                if (item == _folder)
                {
                    _folder = null;
                }

                await Service.DeleteAsync(item.Id);
                await GetFolders();
            }
        }


        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        async Task InsertRow()
        {
            _folder = new Folder();
            await _folderGrid.InsertRow(_folder);
        }

        async void OnCreateRow(Folder item)
        {
            await Service.PostAsync(item);
            await GetFolders();
        }

        private void LanguageRow(int id)
        {
            
            NavigationManager.NavigateTo($"/Settings/Folders/{id}");
        }
    }
}