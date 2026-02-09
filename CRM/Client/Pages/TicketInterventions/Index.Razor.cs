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
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }

       
        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private INavMenuService navMenuService { get; set; }

        [Inject]
        IRestService<ApplicationUser> userSigned { get; set; }

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

        private string _test = "";
        protected override async Task OnInitializedAsync()
        {
            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            _user = await userSigned.Get();
      
            await LoadData();
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

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;

                    _filter.Filter = args?.Filter;


                }

                PagingResponse<TicketIntervention> pagingResponse = await _service.Get(_filter);

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
                    NavigationManager.NavigateTo($"Projects/{IdProject}/Tickets/{IdTicket}/interventions/{id}/info");

                else if (IdCompany != null)
                    NavigationManager.NavigateTo($"Companies/{IdCompany}/Tickets/{IdTicket}/interventions/{id}/info");
                else
                    NavigationManager.NavigateTo($"/Tickets/{IdTicket}/interventions/{id}/info");
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
                    await _service.Delete(id);

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
