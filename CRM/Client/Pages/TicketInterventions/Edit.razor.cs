using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using Syncfusion.Blazor.Calendars;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }

        [Inject]
        private ITicketService _ticketService { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _userService { get; set; }

        [Inject]
        private HttpClient _httpClient { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int IdTicket { get; set; }

       

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private TicketIntervention _ticketIntervention = new TicketIntervention();

        private List<Article> _products = new List<Article>();

        private List<TicketType> _ticketTypes = new List<TicketType>();

        private List<InterventionType> _interventionTypes;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private Ticket _ticket;

        private string _header;

       
        protected override async Task OnInitializedAsync()
        {
            try
            {
                await LoadUsers(new LoadDataArgs());
                _ticket = await _ticketService.Get(IdTicket);

                if (Id != null)
                {
                    _header = "INTERVENTO MODIFICA";
                    _ticketIntervention = await _service.Get(Id.Value);

                    
                }
                else
                {
                    _header = "INTERVENTO NUOVO";
                    _ticketIntervention = new TicketIntervention() { StartDateTime = DateTime.Now, EndDateTime = DateTime.Now, IdUser = _ticket.IdUserAssigned };
                    _ticketIntervention.IdTicket = IdTicket;
                    
                }
               
                await GetTicketTypes();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public async Task LoadUsers(LoadDataArgs args)
        {
            UsersFilterModel request = new UsersFilterModel();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.Name = args.Filter;
            }
            var response = await _userService.Get(request);

            _users = response.Items.ToList();

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
            TicketIntervention resp;

            try
            {
                if (Id == null)
                {
                    //_ticketIntervention.Date = DateTime.Now;
                    _ticketIntervention.IdTicket = IdTicket;
                    
                }
                await _service.Post(_ticketIntervention);
                
                if (OnClickSave != null)
                    OnClickSave();
                else
                    NavigationManager.NavigateTo("/TicketsIntervention/Index");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }
        private void ValueStartDateTimeChangeHandler()
        {
            if (_ticketIntervention.StartDateTime < _ticketIntervention.EndDateTime)
            {
                _ticketIntervention.Minute = (int)(_ticketIntervention.EndDateTime - _ticketIntervention.StartDateTime).TotalMinutes;
            }
        }

        protected void ValueEndDateTimeChangeHandler()
        {
            if (_ticketIntervention.EndDateTime > _ticketIntervention.StartDateTime)
            {
                _ticketIntervention.Minute = (int)(_ticketIntervention.EndDateTime - _ticketIntervention.StartDateTime).TotalMinutes;
            }
        }

        protected async Task GetTicketTypes()
        {
            _interventionTypes = await _httpClient.GetFromJsonAsync<List<InterventionType>>(ConstHelper.InterventionTypesPath);


        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Tickets/Index");
        }

        private TicketInterventionArticleModel ArticlesFind(TicketInterventionArticleModel item)
        {
            TicketInterventionArticleModel article;

            
            article = _ticketIntervention.InterventionArticles.Where(x => x.Id == item.Id).FirstOrDefault();

            return article;
        }
        private void ArticlesOnDelete(TicketInterventionArticleModel item)
        {
            StateHasChanged();
        }

        private void ArticledOnAdd(TicketInterventionArticleModel item)
        {
            TicketInterventionArticleModel article = ArticlesFind(item);

            if (article != null)
                _ticketIntervention.InterventionArticles.Add(article);
        }

        private void ArticledOnUpdate(TicketInterventionArticleModel item)
        {
            TicketInterventionArticleModel article = ArticlesFind(item);

            if (article != null)
            {
                article.IdArticle = item.IdArticle;
                article.IdProduct = item.IdProduct;               
                article.SerialNumber = item.SerialNumber;
            }
        }

       

        private void OnUpdateArticles()
        {
            StateHasChanged();
        }
    }
}
