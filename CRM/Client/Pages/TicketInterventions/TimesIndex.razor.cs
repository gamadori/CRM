using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class TimesIndex: ComponentBase
    {
       
        [Inject]
        HttpClient HttpClient { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public List<TicketInterventionTimeModel> InterventionTimes { get; set; }

        [Parameter]
        public int IdIntervention { get; set; }

        [Parameter]
        public EventCallback<TicketInterventionTimeModel> OnDelete { get; set; }

        [Parameter]
        public EventCallback<TicketInterventionTimeModel> OnUpdate { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public EventCallback<TicketInterventionTimeModel> OnAdd { get; set; }

        [Parameter]
        public EventCallback<List<TicketInterventionTimeModel>> OnChanged { get; set; }

       


       

        private List<TicketInterventionTimeModel> _times;

        private RadzenDataGrid<TicketInterventionTimeModel> _timesGrid;

        private TicketInterventionTimeModel _interventionTime;

        private bool _isLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
            _isLoading = false;
        }

        private async Task LoadData()
        {


           

            await LoadTimes();
             
        }


        private async Task LoadTimes()
        {

            var resp = await HttpClient.GetFromJsonAsync<List<TicketInterventionTimeModel>>(ConstHelper.InterventionTime);

            if (resp != null)
                _times = resp;
            else
                _times = new List<TicketInterventionTimeModel>();

           
            StateHasChanged();

        }

        async Task EditRow(TicketInterventionTimeModel item)
        {
            await _timesGrid.EditRow(item);
            //await LoadArticles(item.IdProduct);
        }

        void OnUpdateRow(TicketInterventionTimeModel item)
        {
            if (item == _interventionTime)
            {
                _interventionTime = null;
            }

            
        }

        async Task SaveRow(TicketInterventionTimeModel item)
        { 
            if (item == _interventionTime)
            {
                _interventionTime = null;
            }
           

            await _timesGrid.UpdateRow(item);
        }

        void CancelEdit(TicketInterventionTimeModel item)
        {
            if (item == _interventionTime)
            {
                _interventionTime = null;
            }

            _timesGrid.CancelEditRow(item);

            
        }

        async Task DeleteRow(TicketInterventionTimeModel item)
        {
            if (item == _interventionTime)
            {
                _interventionTime = null;
            }

            if (await DialogService.Confirm(Localize["Eliminare il Dispositivo?"]) == true)
            {
                if (_times.Contains(item))
                {

                    // For demo purposes only
                    _times.Remove(item);

                    // For production
                    //dbContext.SaveChanges();

                    await _timesGrid.Reload();

                    if (OnDelete.HasDelegate)
                        await OnDelete.InvokeAsync();

                }
                else
                {
                    _timesGrid.CancelEditRow(item);
                }
            }
        }
        void OnCreateRow(TicketInterventionTimeModel item)
        {
            _times.Add(item);
        }

        async Task InsertRow()
        {
            _interventionTime = new TicketInterventionTimeModel() { };
            await _timesGrid.InsertRow(_interventionTime);
        }

        
    }
}
