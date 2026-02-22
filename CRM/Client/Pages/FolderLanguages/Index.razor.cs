using CRM.Client.Helpers;
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
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Client.Pages.FolderLanguages
{
    public partial class Index : ComponentBase
    {
        


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IFolderLanguagesService _service { get; set; }

        [Inject]
        IFoldersService _serviceFolder { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Parameter]
        public int FolderId { get; set; }
        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }



        private FolderLanguageFilter _filter = new FolderLanguageFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<FolderLanguageDTO> _folderGrid;

        private List<FolderLanguageDTO> _folderLanguages = new List<FolderLanguageDTO>();
        private FolderDTO _folder;

        private PagingHeaderModel _paging = new PagingHeaderModel();

    

        private bool _isLoading = false;

        private List<Language> _languages;

        private FolderLanguageDTO _folderLanguage;

        protected override async Task OnInitializedAsync()
        {
            _isLoading = true;
            await GetLanguages();
            await GetFolder();
            await GetFolderLanguages();
            _isLoading = false;
            StateHasChanged();

            await base.OnInitializedAsync();
        }

        private async Task GetFolderLanguages(LoadDataArgs args = null)
        {
            try
            { 
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                }

                _filter.FolderId = FolderId;


                PagingResponse<FolderLanguageDTO> pagingResponse = await _service.GetPagingAsync(_filter);

                if (pagingResponse != null)
                {
                    _folderLanguages = pagingResponse.Items;
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

        private async Task GetFolder()
        {
            _folder = await _serviceFolder.GetItemAsync(FolderId);
        }
        private async Task GetLanguages()
        {
            try
            {
                _languages = await HttpClient.GetFromJsonAsync<List<Language>>($"{ConstHelper.LanguagesPath}/list");

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

            }
        }

        private string? GetFlag(int id)
        {
            // Carica le bandiere per tutte le lingue presenti
            var lang = _languages.FirstOrDefault(x => x.Id == id);
            return lang?.Flag;
        }
        async Task EditRow(FolderLanguageDTO folderLanguage)
        {
            await _folderGrid.EditRow(folderLanguage);
        }

        async Task OnUpdateRow(FolderLanguageDTO item)
        {
            if (item == _folderLanguage)
            {
                _folderLanguage = null;
            }
            var resp = await _service.PostAsync(new FolderLanguage()
            {
                Id = item.Id,
                FolderId = item.IdFolder,
                LanguageId = item.IdLanguage,
                Name = item.Name
            });

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);

            }
            else
                Notify(Localize["Dato Aggiornato"], NotificationSeverity.Success);


        }

        private async Task SaveRow(FolderLanguageDTO item)
        {
            if (item == _folderLanguage)
            {
                _folderLanguage = null;
            }

            await _folderGrid.UpdateRow(item);

           // await GetInterventions();
        }

        private void CancelEdit(FolderLanguageDTO item)
        {
            if (item == _folderLanguage)
            {
                _folderLanguage = null;
            }

            _folderGrid.CancelEditRow(item);

           
        }

        async Task DeleteRow(FolderLanguageDTO item)
        {
            if (await DialogService.Confirm(Localize["Eliminare il Tipo di intervento?"], Localize["Elimina"]))
            {
                if (item == _folderLanguage)
                {
                    _folderLanguage = null;
                }

                await _service.DeleteAsync(item.Id);
                await GetFolderLanguages();
            }
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        async Task InsertRow()
        {
            _folderLanguage = new FolderLanguageDTO() {  IdFolder = FolderId};
            await _folderGrid.InsertRow(_folderLanguage);
        }

        async Task OnCreateRow(FolderLanguageDTO item)
        {
            await _service.PostAsync(new FolderLanguage()
            {
                FolderId = item.IdFolder,
                LanguageId = item.IdLanguage,
                Name = item.Name
            });

            await GetFolderLanguages();
            
        }

    }
}
