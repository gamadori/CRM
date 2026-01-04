using CRM.Client.Helpers;
using CRM.Client.Pages.Projects;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Info : ComponentBase
    {
        public enum TicketViews
        {
            Ticket,
            Allegati,
            Chiusura,
            Interventi,
            Chat,
            Print
        }

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New,
            AddFiles,
            PDFViewer,
            
            Null

        }

        [Inject]
        NavigationManager NavigationManager { get; set; }


        [Inject]
        ITicketService _service { get; set; }

        
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }
        
        [Inject]
        IUserService _userService { get; set; }

        [Inject]
        IBreadCrumbService _breadCrumbService { get; set;}

        [Inject] 
        IJSRuntime JsRuntime { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int? IdProject { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public int TypeSearch { get; set; } = (int)(TicketTypeSearch.All);

        [Parameter]
        public string? IdUser { get; set; } = null;

        [Parameter]
        public int? IdAttachment { get; set; }

        [Parameter]
        public int? IdChat { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        protected TicketViews selectView = TicketViews.Ticket;

        private PartialViews _partialView = PartialViews.Details;



        private int? _idTicket;

        private int? _idAttachment;

        private int? _idIntervention;

        private Ticket _ticket = null;

        private bool _fromDetails = false;

        private List<BreadcrumbItem> _bread = new List<BreadcrumbItem>();

        private Project _project;

        private Company _company;

        private ApplicationUser _user;

        private bool singleValue = false;

        private bool _isMobile = false;
        private ButtonSize _buttonSize = ButtonSize.Medium;

        //private async Task GotoIndex(object p)
        //{
        //    if (OnGotoIndex != null)
        //        OnGotoIndex();
        //}
        protected override async Task OnInitializedAsync()
        {
            await FindResponsiveness();
            await LoadTicket();


            
            if (IdProject != null)
            {
                await GetProject();
                _bread.Add(new BreadcrumbItem() { Text = $"{Localize["Progetto"]}: {_project?.Name}", Url = $"/Projects/{IdProject}" });
                _bread.Add(new BreadcrumbItem() { Text = $"{Localize["Tickets"]}", Url = $"/Projects/{IdProject}/{(int)ProjectViews.Tickets}" });
            }
            else if (IdCompany != null)
            {
                
                await GetCompany();
                _bread.Add(new BreadcrumbItem() { Text = $"{Localize["Company"]}: {_company?.RagioneSociale}", Url = $"/Companies/{IdCompany}" });
                _bread.Add(new BreadcrumbItem() { Text = $"{Localize["Tickets"]}", Url = $"/Companies/{IdCompany}/{(int)CompanyViews.Ticket}" });

            }
           
            else if (IdUser != null)
            {
                
                _bread =  _breadCrumbService.TicketAssigned(IdUser, (TicketTypeSearch)TypeSearch);

                
            }
            else if (TypeSearch != (int)TicketTypeSearch.All)
            {
                _bread =  _breadCrumbService.TicketFiltered(IdUser, (TicketTypeSearch)TypeSearch);
            }
            else
            {
                _bread.Add(new BreadcrumbItem() { Text = Localize["Tickets"], Url = "/Tickets" });
            }
            _bread.Add(new BreadcrumbItem() { Text = $"{Localize["Ticket n"]} {_ticket?.Id}", Url = null });

            if (IdAttachment != null)
            {
                selectView = TicketViews.Allegati;
                _idAttachment = IdAttachment;
                _partialView = PartialViews.Details;
            }
            else if (IdChat != null)
            {
                selectView = TicketViews.Chat;
               
                _partialView = PartialViews.Details;
            }
            
        }

        private async Task GetProject()
        {
            _project = await RestClientService.GetItem<Project, int>((int)IdProject, ConstHelper.ProjectsPath);

        }

        private async Task GetCompany()
        {
            _company = await RestClientService.GetItem<Company, int>((int)IdCompany, ConstHelper.CompaniesPath);

        }

        private async Task GetUser()
        {
            _user = await _userService.GetItem<ApplicationUser, string>(IdUser, ConstHelper.UsersPath);
        }
        private async Task LoadTicket()
        {
            _ticket = await _service.Get(Id);

            
        }


        private void EditTicket()
        {
            _fromDetails = false;
            selectView = TicketViews.Ticket;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelTicket()
        {
            selectView = TicketViews.Ticket;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveTicket()
        {
            selectView = TicketViews.Ticket;
            _partialView = PartialViews.Details;
            await LoadTicket();
            StateHasChanged();
        }

        private void CloseTicket()
        {
            _fromDetails = true;
            selectView = TicketViews.Chiusura;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        private void PrintTicket()
        {
            selectView = TicketViews.Print;
            StateHasChanged();

        }

        #region Chiusura
        void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else if (IdCompany != null)
            {
                NavigationManager.NavigateTo($"/Companies/{IdCompany}/{(int)CompanyViews.Ticket}");
            }
            else if (TypeSearch != (int)TicketTypeSearch.All)
            {
                NavigationManager.NavigateTo($"/Tickets/Index/{TypeSearch}/{IdUser}");
            }
            else
                NavigationManager.NavigateTo($"/Tickets");
        }

        private void EditClosing(int? id)
        {
            selectView = TicketViews.Chiusura;
            _partialView = PartialViews.Edit;
            _idTicket = id;

            StateHasChanged();
        }

        private void IndexEditClosing(int? id)
        {
            _fromDetails = false;
            EditClosing(id);
        }

        private void DetailsIntervento(int id)
        {
            selectView = TicketViews.Chiusura;
            _partialView = PartialViews.Details;
            _idTicket = id;

            StateHasChanged();
        }

        private void DetailsEditClosing()
        {
            _fromDetails = true;
            EditClosing(_idTicket);
        }

        private async void ClosingCloseForm()
        {
            selectView = TicketViews.Ticket;
            _partialView = PartialViews.Details;

            await LoadTicket();
            StateHasChanged();
        }

        private async void UpdateForm()
        {
            await LoadTicket();
            StateHasChanged();
        }

        private async void OnRefresh()
        {
            await LoadTicket();
            StateHasChanged();
        }
        #endregion

        #region Interventi

        private void InterventionIndex()
        {
            selectView = TicketViews.Interventi;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }

        private void InterventionEdit(int? id)
        {
            selectView = TicketViews.Interventi;
            _partialView = PartialViews.Edit;
            _idIntervention = id;

            StateHasChanged();
        }

        private void InterventionIndexEdit(int? id)
        {
            _fromDetails = false;
            InterventionEdit(id);
        }

        private void InterventionDetails(int id)
        {
            selectView = TicketViews.Interventi;
            _partialView = PartialViews.Details;
            _idIntervention = id;

            StateHasChanged();
        }


        private void InterventionDetailsEdit()
        {
            _fromDetails = true;
            InterventionEdit(_idIntervention);
        }

        private void InterventionCloseForm()
        {
            selectView = TicketViews.Interventi;
            if (_fromDetails && _partialView != PartialViews.Details)
            {
                _partialView = PartialViews.Details;
            }
            else
            {
                _partialView = PartialViews.Index;
            }

            StateHasChanged();
        }
        #endregion

        #region Attachments

        private void DetailsAttachment(int id)
        {
            selectView = TicketViews.Allegati;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            selectView = TicketViews.Allegati;
            _partialView = PartialViews.Edit;
            _idAttachment = id;

            StateHasChanged();
        }


        private void IndexDetailsAttachment(int id)
        {
            _fromDetails = true;
            DetailsAttachment(id);
        }
        private void IndexEditAttachment(int? id)
        {
            _fromDetails = false;
            EditAttachment(id);
        }

        private void DetailsEditAttachment(int? id)
        {
            _fromDetails = true;
            EditAttachment(id);
        }

        private void AddFileAttachment()
        {
            selectView = TicketViews.Allegati;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

        private void InterventionPDFViewer()
        {
            selectView = TicketViews.Interventi;
            _partialView = PartialViews.PDFViewer;
            StateHasChanged();
        }

        private void InterventionPDFViewerUpdate()
        {
            selectView = TicketViews.Interventi;
            _partialView = PartialViews.Null;
            StateHasChanged();
            InterventionPDFViewer();
        }

       
        private void CloseAttachment()
        {
            selectView = TicketViews.Allegati;

            if (_fromDetails && _partialView != PartialViews.Details)
            {
                _partialView = PartialViews.Details;
            }
            else
            { 
                _partialView = PartialViews.Index;
            }
            StateHasChanged();

        }

       


        #endregion
        void Change(object value, string name)
        {
            if (selectView == TicketViews.Ticket)
                _partialView = PartialViews.Details;
            else
            {
                _fromDetails = false;
                _partialView = PartialViews.Index;
            }
            StateHasChanged();
        }
        public async Task FindResponsiveness()
        {
            _isMobile = await JsRuntime.InvokeAsync<bool>("isDevice");

            if (_isMobile)
                _buttonSize = ButtonSize.Small;
        }

    }
}
