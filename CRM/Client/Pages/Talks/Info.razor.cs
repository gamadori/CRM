using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Talks
{
    public enum TalksViews
    {
        Talk,
        Attachments,
        Chat,
        Tickets
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
        private ITalkService _service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int SelectView
        {
            get { return (int)_selectView; }
            set { _selectView = (TalksViews)value; }
        }

        private TalksViews _selectView  = TalksViews.Talk;

        private PartialViews _partialView = PartialViews.Details;

        private int? _idTalk;

        private int? _idTicket;

        private int? _idAttachment;


        private Talk _talk = null;

        private bool _fromDetails = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        bool singleValue = false;

        protected override async Task OnInitializedAsync()
        {
            
            await LoadData();

            _bread = await BreadCrumbService.Talk(false);

            await base.OnInitializedAsync();
            
            
        }

        private async Task LoadData()
        {
            _talk = await _service.Get(Id);
        }
        private void EditTalk()
        {
            _fromDetails = false;
            _selectView = TalksViews.Talk;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelTalk()
        {
            _selectView = TalksViews.Talk;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveTalk()
        {
            _selectView = TalksViews.Talk;
            _partialView = PartialViews.Details;
            await LoadData();
            StateHasChanged();
        }

        


    
        #region Attachments

        private void DetailsAttachment(int id)
        {
            _selectView = TalksViews.Attachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            _selectView = TalksViews.Attachments;
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
            _selectView = TalksViews.Attachments;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

       

       
        private void CloseAttachment()
        {
            _selectView = TalksViews.Attachments;

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
            if (_selectView == TalksViews.Talk)
                _partialView = PartialViews.Details;
            else
            {
                _fromDetails = false;
                _partialView = PartialViews.Index;
            }
            StateHasChanged();
        }

        private void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else
                NavigationManager.NavigateTo($"/Talks");
        }

    }
}
