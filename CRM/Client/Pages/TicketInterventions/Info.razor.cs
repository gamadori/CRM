using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Pages.Projects;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Info : ComponentBase
    {
        public enum InterventionViews
        {
            Intervention,
            Allegati,
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
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }
        
        [Inject]
        IUserService _userService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject] 
        IJSRuntime JsRuntime { get; set; }


        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int? IdTicket { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public int? IdProject { get; set; }

        [Parameter]
        public int? IdArticle { get; set; }
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

        [Parameter]
        public int? IdIntervention { get; set; } = null;

        protected InterventionViews selectView = InterventionViews.Intervention;

        private PartialViews _partialView = PartialViews.Details;


        private int? _idTicket;

        private int? _idAttachment;

        private int? _idIntervention;

        private TicketIntervention _intervention = null;

        private Ticket _ticket = null;

        private bool _fromDetails = false;

        private Project _project;

        private Company _company;

        private ApplicationUser _user;

        private bool singleValue = false;

        private bool _isMobile = false;

        private ButtonSize _buttonSize = ButtonSize.Medium;

        private List<ViewOption<InterventionViews>> _viewOptions;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            
            await FindResponsiveness();

            await LoadIntervention();
            
            if (IdProject != null)
            {
                await GetProject();
            }
            else if (IdCompany != null)
            {
                await GetCompany();
            }
            
            if (IdAttachment != null)
            {
                selectView = InterventionViews.Allegati;
                _idAttachment = IdAttachment;
                _partialView = PartialViews.Details;
            }
            
            _pageHeader = await HeaderService.Create();

            InitializeViewOptions();
        }

        private void InitializeViewOptions()
        {
            _viewOptions = new List<ViewOption<InterventionViews>>
            {
                new ViewOption<InterventionViews> { Text = Localize["Intervention Data"], Value = InterventionViews.Intervention },
                new ViewOption<InterventionViews> { Text = Localize["Attachments"], Value = InterventionViews.Allegati },
            };
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
        private async Task LoadIntervention()
        {
            _intervention = await _service.Get(Id);
            
        }

        private void EditIntervention()
        {
            _fromDetails = false;
            selectView = InterventionViews.Intervention;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelIntervention()
        {
            selectView = InterventionViews.Intervention;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveIntervention()
        {
            selectView = InterventionViews.Intervention;
            _partialView = PartialViews.Details;
            await LoadIntervention();
            StateHasChanged();
        }

        void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else
            {
                var url = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
                url = System.Text.RegularExpressions.Regex.Replace(url, "info", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var pos = url.LastIndexOf("intervention");
                if (pos > 0)
                {
                    url = url.Substring(0, pos);
                }
                NavigationManager.NavigateTo($"{url}?view=interventions");

            }
        }


        #region Attachments

        private void DetailsAttachment(int id)
        {
            selectView = InterventionViews.Allegati;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            selectView = InterventionViews.Allegati;
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
            selectView = InterventionViews.Allegati;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

       
        private void CloseAttachment()
        {
            selectView = InterventionViews.Allegati;

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
            if (selectView == InterventionViews.Intervention)
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
