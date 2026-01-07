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

namespace CRM.Client.Pages.InterventionTypeLanguages
{
    public partial class Index : ComponentBase
    {
        private const string PageFolder = "InterventionTypes";


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IBaseRestService<InterventionTypeLanguage, InterventionTypeLangFilter, int> _service { get; set; }

        [Inject]
        IBaseRestService<InterventionType, InterventionTypeFilter, int> _serviceInt { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Parameter]
        public int IdInterventionType { get; set; }
        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }



        private InterventionTypeLangFilter _filter = new InterventionTypeLangFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<InterventionTypeLanguage> _interventionGrid;

        private List<InterventionTypeLanguage> _interventionTypeLangs = new List<InterventionTypeLanguage>();

        private InterventionTypeLanguage _interventionTypeLang;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private InterventionTypeLangFilter _pa = new InterventionTypeLangFilter();

        private bool _isLoading = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private List<Language> _languages;

        private InterventionType _interventionType;

        protected override async Task OnInitializedAsync()
        {
            await GetInterventions();
            await GetLanguages();
            await GetInterventionType();

            _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "Settings" });
            _bread.Add(new BreadcrumbModel() { Title = Localize["Tipi Intervento"], Url = "Settings/InterventionTypes" });
            _bread.Add(new BreadcrumbModel() { Title = _interventionType.Name, Url = null });

            await base.OnInitializedAsync();
        }

        private async Task GetInterventions(LoadDataArgs args = null)
        {
            try
            { 
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                }

                _filter.IdInterventionType = IdInterventionType;


                PagingResponse<InterventionTypeLanguage> pagingResponse = await _service.Get(_filter);

                if (pagingResponse != null)
                {
                    _interventionTypeLangs = pagingResponse.Items;
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

        private async Task GetInterventionType()
        {
            _interventionType = await _serviceInt.Get(IdInterventionType);
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
       

        async Task EditRow(InterventionTypeLanguage interventionType)
        {
            await _interventionGrid.EditRow(interventionType);
        }

        async Task OnUpdateRow(InterventionTypeLanguage item)
        {
            if (item == _interventionTypeLang)
            {
                _interventionTypeLang = null;
            }
            var resp = await _service.Post(item);

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);

            }
            else
                Notify(Localize["Dato Aggiornato"], NotificationSeverity.Success);


        }

        private async Task SaveRow(InterventionTypeLanguage item)
        {
            if (item == _interventionTypeLang)
            {
                _interventionTypeLang = null;
            }

            await _interventionGrid.UpdateRow(item);

           // await GetInterventions();
        }

        private async Task CancelEdit(InterventionTypeLanguage item)
        {
            if (item == _interventionTypeLang)
            {
                _interventionTypeLang = null;
            }

            _interventionGrid.CancelEditRow(item);

           
        }

        async Task DeleteRow(InterventionTypeLanguage item)
        {
            if (await DialogService.Confirm(Localize["Eliminare il Tipo di intervento?"], Localize["Elimina"]))
            {
                if (item == _interventionTypeLang)
                {
                    _interventionTypeLang = null;
                }

                await _service.Delete(item.Id);
                await GetInterventions();
            }
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        async Task InsertRow()
        {
            _interventionTypeLang = new InterventionTypeLanguage() {  IdInterventionType = IdInterventionType};
            await _interventionGrid.InsertRow(_interventionTypeLang);
        }

        async Task OnCreateRow(InterventionTypeLanguage item)
        {
            await _service.Post(item);

            await GetInterventions();
            
        }
    }
}
