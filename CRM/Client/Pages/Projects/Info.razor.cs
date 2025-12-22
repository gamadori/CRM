using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Projects
{
    public enum ProjectViews
    {
        Project,
        Attachments,
        Chat,
        Tickets, 
        User
    }

    public partial class Info : ComponentBase
    {
        

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
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IProjectsService ProjectsService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int SelectView
        {
            get { return (int)_selectView; }
            set { _selectView = (ProjectViews)value; }
        }

        private ProjectViews _selectView  = ProjectViews.Project;

        private PartialViews _partialView = PartialViews.Details;

        private int? _idProject;

        private int? _idTicket;

        private int? _idAttachment;


        private Project _project = null;

        private bool _fromDetails = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        bool singleValue = false;

        protected override async Task OnInitializedAsync()
        {
            
            await LoadProject();
            _bread.Add(new BreadcrumbModel() { Title = Localize["Progetti"], Url = "/Projects" });
            _bread.Add(new BreadcrumbModel() { Title = $"{Localize["Progetto: "]} {_project?.Name}", Url = null });

            if (_selectView == ProjectViews.Tickets)
                _partialView = PartialViews.Index;
        }

        private async Task LoadProject()
        {
            _project = await RestClientService.GetItem<Project, int>(Id, ConstHelper.ProjectsPath); 
        }
        private void EditProject()
        {
            _fromDetails = false;
            _selectView = ProjectViews.Project;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelProject()
        {
            _selectView = ProjectViews.Project;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveProject()
        {
            _selectView = ProjectViews.Project;
            _partialView = PartialViews.Details;
            await LoadProject();
            StateHasChanged();
        }

        private  void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else
                NavigationManager.NavigateTo($"/Projects");
        }


        #region Tickets

        private void TicketsIndex()
        {
            _selectView = ProjectViews.Tickets;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }

        private void TicketEdit(int? id)
        {
            _selectView = ProjectViews.Tickets;
            _partialView = PartialViews.Edit;
            _idTicket = id;

            StateHasChanged();
        }

        private void TicketIndexEdit(int? id)
        {
            _fromDetails = false;
            TicketEdit(id);
        }

        private void TicketDetails(int id)
        {
            _selectView = ProjectViews.Tickets;
            _partialView = PartialViews.Details;
            _idTicket = id;

            StateHasChanged();
        }


        private void TicketDetailsEdit()
        {
            _fromDetails = true;
            TicketEdit(_idTicket);
        }

        private void TicketCloseForm()
        {
            _selectView = ProjectViews.Tickets;
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
            _selectView = ProjectViews.Attachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            _selectView = ProjectViews.Attachments;
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
            _selectView = ProjectViews.Attachments;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

       

       
        private void CloseAttachment()
        {
            _selectView = ProjectViews.Attachments;

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

        #region Users
        private async Task AddUser(string idUser)
        {
            if (idUser != null)
            {
                await ProjectsService.AddUser(new ProjectUser() { IdProject = Id, IdUser = idUser });

                StateHasChanged();
            }
        }

        private async Task RemoveUser(string idUser)
        {
            if (await DialogService.Confirm(Localize["Remove from users?"]) == true)
            {
                await ProjectsService.RemoveUser(new ProjectUser() { IdProject = Id, IdUser = idUser });

                StateHasChanged();
            }
        }

        #endregion
        void Change(object value, string name)
        {
            if (_selectView == ProjectViews.Project)
                _partialView = PartialViews.Details;
            else
            {
                _fromDetails = false;
                _partialView = PartialViews.Index;
            }
            StateHasChanged();
        }

        
    }
}
