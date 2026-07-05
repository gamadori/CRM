using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
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

namespace CRM.Client.Pages.AccessoryTypeLangs
{
    public partial class Index : ComponentBase
    {
        


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IAccessoryTypeLanguagesService _service { get; set; }

        [Inject]
        IAccessoryTypesService _serviceAccessory { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Parameter]
        public int IdAccessoryType { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }



        private AccessoryTypeLanguageFilter _filter = new AccessoryTypeLanguageFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<AccessoryTypeLanguage> _accessoryGrid;

        private List<AccessoryTypeLanguage> _accessoryTypeLangs = new List<AccessoryTypeLanguage>();

        private AccessoryTypeLanguage _accessoryTypeLang;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private AccessoryTypeLanguageFilter _pa = new AccessoryTypeLanguageFilter();

        private bool _isLoading = false;

        private List<Language> _languages;

        private AccessoryType _accessoryType;

        protected override async Task OnInitializedAsync()
        {
            await GetAccessories();
            await GetLanguages();
            await GetAccessoryType();

            await base.OnInitializedAsync();
        }

        private async Task GetAccessories(LoadDataArgs args = null)
        {
            try
            { 
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                }

                _filter.IdAccessoryType = IdAccessoryType;


                PagingResponse<AccessoryTypeLanguage> pagingResponse = await _service.Get(_filter);

                if (pagingResponse != null)
                {
                    _accessoryTypeLangs = pagingResponse.Items;
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

        private async Task GetAccessoryType()
        {
            _accessoryType = await _serviceAccessory.Get(IdAccessoryType);
        }
        private async Task GetLanguages()
        {
            try
            {
                _languages = await HttpClient.GetFromJsonAsync<List<Language>>(ConstHelper.LanguagesPath);
                
                
                
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


        async Task EditRow(AccessoryTypeLanguage accessoryType)
        {
            await _accessoryGrid.EditRow(accessoryType);
        }

        async Task OnUpdateRow(AccessoryTypeLanguage item)
        {
            if (item == _accessoryTypeLang)
            {
                _accessoryTypeLang = null;
            }
            var resp = await _service.Post(item);

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);

            }
            else
                Notify(Localize["Dato Aggiornato"], NotificationSeverity.Success);


        }

        private async Task SaveRow(AccessoryTypeLanguage item)
        {
            if (item == _accessoryTypeLang)
            {
                _accessoryTypeLang = null;
            }

            await _accessoryGrid.UpdateRow(item);

           // await GetInterventions();
        }

        private async Task CancelEdit(AccessoryTypeLanguage item)
        {
            if (item == _accessoryTypeLang)
            {
                _accessoryTypeLang = null;
            }

            _accessoryGrid.CancelEditRow(item);

           
        }

        async Task DeleteRow(AccessoryTypeLanguage item)
        {
            if (await DialogService.Confirm(Localize["Delete the accessory Type selected?"], Localize["Elimina"]))
            {
                if (item == _accessoryTypeLang)
                {
                    _accessoryTypeLang = null;
                }

                await _service.Delete(item.Id);
                await GetAccessories();
            }
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        async Task InsertRow()
        {
            _accessoryTypeLang = new AccessoryTypeLanguage() {  IdAccessoryType = IdAccessoryType };
            await _accessoryGrid.InsertRow(_accessoryTypeLang);
        }

        async Task OnCreateRow(AccessoryTypeLanguage item)
        {
            await _service.Post(item);

            await GetAccessories();
            
        }
    }
}
