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

namespace CRM.Client.Pages.ProdAccTypeLangs
{
    public partial class Index : ComponentBase
    {
        


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IProductAccTypesLangService Service { get; set; }

        [Inject]
        IProductAccTypesService ProductAccTypesService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Parameter]
        public int IdProdAccType { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }



        private ProductAccessoryTypeLangFilter _filter = new ProductAccessoryTypeLangFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<ProductAccessoryTypeLang> _accessoryGrid;

        private List<ProductAccessoryTypeLang> _prodAccTypeLangs = new List<ProductAccessoryTypeLang>();

        private ProductAccessoryTypeLang _prodAccTypeLang;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private AccessoryTypeLanguageFilter _pa = new AccessoryTypeLanguageFilter();

        private bool _isLoading = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private List<Language> _languages;

        private ProductAccessoryType _prodAccType;

        protected override async Task OnInitializedAsync()
        {
            await GetProdAccs();
            await GetLanguages();
            await GetProdAccType();

            _bread = await BreadCrumbService.AccessoryTypes(_prodAccType?.Name, false);
            
            await base.OnInitializedAsync();
        }

        private async Task GetProdAccs(LoadDataArgs args = null)
        {
            try
            { 
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                }

                _filter.IdProdAccType = IdProdAccType;


                PagingResponse<ProductAccessoryTypeLang> pagingResponse = await Service.Get(_filter);

                if (pagingResponse != null)
                {
                    _prodAccTypeLangs = pagingResponse.Items;
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

        private async Task GetProdAccType()
        {
            _prodAccType = await ProductAccTypesService.Get(IdProdAccType);
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


        async Task EditRow(ProductAccessoryTypeLang item)
        {
            await _accessoryGrid.EditRow(item);
        }

        async Task OnUpdateRow(ProductAccessoryTypeLang item)
        {
            if (item == _prodAccTypeLang)
            {
                _prodAccTypeLang = null;
            }
            var resp = await Service.Post(item);

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);

            }
            else
                Notify(Localize["Dato Aggiornato"], NotificationSeverity.Success);


        }

        private async Task SaveRow(ProductAccessoryTypeLang item)
        {
            if (item == _prodAccTypeLang)
            {
                _prodAccTypeLang = null;
            }

            await _accessoryGrid.UpdateRow(item);

           // await GetInterventions();
        }

        private async Task CancelEdit(ProductAccessoryTypeLang item)
        {
            if (item == _prodAccTypeLang)
            {
                _prodAccTypeLang = null;
            }

            _accessoryGrid.CancelEditRow(item);

           
        }

        async Task DeleteRow(ProductAccessoryTypeLang item)
        {
            if (await DialogService.Confirm(Localize["Delete the Translate selected?"], Localize["Elimina"]))
            {
                if (item == _prodAccTypeLang)
                {
                    _prodAccTypeLang = null;
                }

                await Service.Delete(item.Id);
                await GetProdAccs();
            }
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        async Task InsertRow()
        {
            _prodAccTypeLang = new ProductAccessoryTypeLang() {  IdProdAccType = IdProdAccType };
            await _accessoryGrid.InsertRow(_prodAccTypeLang);
        }

        async Task OnCreateRow(ProductAccessoryTypeLang item)
        {
            await Service.Post(item);

            await GetProdAccs();
            
        }
    }
}
