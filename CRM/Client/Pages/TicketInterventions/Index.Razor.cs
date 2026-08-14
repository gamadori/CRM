using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        
        [Inject]
        ITicketInterventionsService ServiceInterventions { get; set; }

        [Inject]
        ICurrentUserService userSigned { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> ServiceUsers { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public string MessagePrepareDelete { get; set; }

        [Parameter]
        public bool CmdDetails { get; set; } = true;

        [Parameter]
        public bool CmdEdit { get; set; } = true;

        [Parameter]
        public bool CmdDelete { get; set; } = true;

        [Parameter]
        public int? IdTicket { get; set; } = null;

        [Parameter] 
        public int? IdProject { get; set; } = null;

        [Parameter]
        public int? IdCompany { get; set; } = null;


        [Parameter]
        public string PageTitle { get; set; } = "Interventi";

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private IQueryable<TicketIntervention> _ticketInterventions = null;


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private TicketInterventionFilter _filter = new TicketInterventionFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private string _pageMessge = "";

        private string _messageDelete = "";

        private ApplicationUser? _user;

        private TicketIntervention _ticketIntervention;

        private string pagingSummaryFormat;

        private int _interventionPageSize = 10;

        private bool _isLoading = false;

        private RadzenDataGrid<TicketIntervention> grdInterventions;

        private string _filterUserName = null;

        private int? _supportTypes;

        /// <summary>
        /// Chi e quando: i due filtri della domanda "cosa ha fatto Tizio in quel giorno". Vanno al
        /// server, non alla griglia.
        /// </summary>
        private string _idUserFiltro = null;

        private DateTime? _giornoFiltro = null;

        /// <summary>Utenti fra cui scegliere. Caricati una volta sola, all'apertura.</summary>
        private List<ApplicationUser> _utenti = new();

        private string _test = "";
        protected override async Task OnInitializedAsync()
        {
            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            _user = await userSigned.Get();

            // Il selettore utente serve solo nell'elenco generale: dentro un ticket si guardano
            // gli interventi di quel ticket, chiunque li abbia fatti.
            if (IdTicket == null)
                await CaricaUtenti();

            await LoadData();
        }

        private async Task CaricaUtenti()
        {
            try
            {
                var risposta = await ServiceUsers.Get(new UsersFilterModel { PageSize = 200 });
                _utenti = risposta.Items.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti: {ex.Message}");
            }
        }

        /// <summary>Cambia chi o quando e ricarica dal server.</summary>
        private async Task CambiaFiltroLavoro()
        {
            _filter.Skip = 0;
            await LoadData();
            StateHasChanged();
        }

        private async Task AzzeraFiltroLavoro()
        {
            _idUserFiltro = null;
            _giornoFiltro = null;
            await CambiaFiltroLavoro();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            try
            {
                await GetInterventions(args);
            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;
            }
            finally
            {
                if (_ticketInterventions == null)
                    _ticketInterventions = Enumerable.Empty<TicketIntervention>().AsQueryable();


            }

        }



        public async Task GetInterventions(LoadDataArgs args = null)
        {
            try
            {


                _filter.IdTicket = IdTicket;
                _filter.IdUser = _idUserFiltro;

                // Un giorno solo: dall'inizio della giornata all'inizio di quella dopo.
                _filter.DateFrom = _giornoFiltro?.Date;
                _filter.DateTo = _giornoFiltro?.Date.AddDays(1);

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;

                    _filter.Filter = args?.Filter;


                }

                PagingResponse<TicketIntervention> pagingResponse = await ServiceInterventions.Get(_filter);

                if (pagingResponse != null)
                {
                    _ticketInterventions = pagingResponse.Items.AsQueryable();
                    _paging = pagingResponse.MetaData;
                }
                else
                    _pageMessge = "Errore";



            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {

                _pageMessge = ex.Message;

            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;

            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.DateFrom = null;
                _filter.DateTo = null;
            }
            StateHasChanged();
        }


        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
            {
                
                if (IdProject != null)
                    NavigationManager.NavigateTo($"Projects/{IdProject}/Tickets/{IdTicket}/interventions/{id}");

                else if (IdCompany != null)
                    NavigationManager.NavigateTo($"Companies/{IdCompany}/Tickets/{IdTicket}/interventions/{id}");
                else
                    NavigationManager.NavigateTo($"/Tickets/{IdTicket}/interventions/{id}");
            }

          
        }



        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/TicketInterventions/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo("/TicketInterventions");
        }
        protected void NewItem()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo("/TicketInterventions/Edit");
        }

   

        protected async Task Delete(int id)
        {

            if (await DialogService.Confirm(Localize["Eliminare il ticjet selezionato"], Localize["Elimina"]) == true)
            { 
                if (OnClickDelete != null)
                    OnClickDelete(id);
                else
                {
                    await ServiceInterventions.Delete(id);

                    await LoadData();
                    StateHasChanged();
                }
            }
        }


       
        void OnChangeCompany(object value, string name)
        {
            var str = value;
        }

        private async void OnCloseFilter()
        {
          //  await JSRuntime.InvokeVoidAsync("Radzen.closePopup", $"popup{grdInterventions.UniqueID}SupportType");
          //  StateHasChanged();

            
        }

        protected void ClearFilter(DataGridColumnFilterEventArgs<TicketIntervention> column)
        {
        
        }
    }
}
