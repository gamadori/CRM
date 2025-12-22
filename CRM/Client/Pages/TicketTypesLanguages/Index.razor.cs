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

namespace CRM.Client.Pages.TicketTypesLanguages
{
    public partial class Index : ComponentBase
    {
        private const string PageFolder = "InterventionTypes";


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        ITicketTypeLanguageService _service { get; set; }

        [Inject]
        ITicketTypesService _serviceTicketType { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Parameter]
        public int IdTicketType { get; set; }
        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }



        private TicketTypeLanguageFilter _filter = new TicketTypeLanguageFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<TicketTypeLanguage> _ticketTypeGrid;

        private List<TicketTypeLanguage> _ticketTypeLangs = new List<TicketTypeLanguage>();

        private TicketTypeLanguage _ticketTypeLang;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        

        private bool _isLoading = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private List<Language> _languages;

        private TicketType _ticketType;

        protected override async Task OnInitializedAsync()
        {
            await GetInterventions();
            await GetLanguages();
            await GetInterventionType();

            _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "Settings" });
            _bread.Add(new BreadcrumbModel() { Title = Localize["Tipi Ticket"], Url = "Settings/TicketTypes" });
            _bread.Add(new BreadcrumbModel() { Title = _ticketType.Desc, Url = null });

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

                _filter.IdTicketType = IdTicketType;


                PagingResponse<TicketTypeLanguage> pagingResponse = await _service.Get(_filter);

                if (pagingResponse != null)
                {
                    _ticketTypeLangs = pagingResponse.Items;
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
            _ticketType = await _serviceTicketType.Get(IdTicketType);
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


        async Task EditRow(TicketTypeLanguage ticketType)
        {
            await _ticketTypeGrid.EditRow(ticketType);
        }

        async Task OnUpdateRow(TicketTypeLanguage item)
        {
            if (item == _ticketTypeLang)
            {
                _ticketTypeLang = null;
            }
            var resp = await _service.Post(item);

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);

            }
            else
                Notify(Localize["Dato Aggiornato"], NotificationSeverity.Success);


        }

        private async Task SaveRow(TicketTypeLanguage item)
        {
            if (item == _ticketTypeLang)
            {
                _ticketTypeLang = null;
            }

            await _ticketTypeGrid.UpdateRow(item);

           // await GetInterventions();
        }

        private async Task CancelEdit(TicketTypeLanguage item)
        {
            if (item == _ticketTypeLang)
            {
                _ticketTypeLang = null;
            }

            _ticketTypeGrid.CancelEditRow(item);

           
        }

        async Task DeleteRow(TicketTypeLanguage item)
        {
            if (await DialogService.Confirm(Localize["Eliminare il Tipo di Ticket?"], Localize["Elimina"]))
            {
                if (item == _ticketTypeLang)
                {
                    _ticketTypeLang = null;
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
            _ticketTypeLang = new TicketTypeLanguage() {  IdTicketType = IdTicketType};
            await _ticketTypeGrid.InsertRow(_ticketTypeLang);
        }

        async Task OnCreateRow(TicketTypeLanguage item)
        {
            await _service.Post(item);

            await GetInterventions();
            
        }

        

    }
}
