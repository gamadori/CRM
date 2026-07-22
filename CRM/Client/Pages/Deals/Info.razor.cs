using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Deals
{
    public enum DealsViews
    {
        Deal,
        Attachments,
        Chat,
        Tickets,
        Quotes,
        Activities
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
        private IDealService _service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int SelectView
        {
            get { return (int)_selectView; }
            set { _selectView = (DealsViews)value; }
        }

        private DealsViews _selectView  = DealsViews.Deal;

        private PartialViews _partialView = PartialViews.Details;

        private int? _idDeal;

        private int? _idTicket;

        private int? _idAttachment;


        private DealDTO _deal = null;

        private bool _fromDetails = false;

        private PageHeaderModel _pageHeader = null;

        bool singleValue = false;

        protected override async Task OnInitializedAsync()
        {

            await LoadData();

            _pageHeader = await HeaderService.Create();

            await base.OnInitializedAsync();
            
            
        }

        private async Task LoadData()
        {
            _deal = await _service.GetItemAsync(Id);
        }
        private void EditDeal()
        {
            _fromDetails = false;
            _selectView = DealsViews.Deal;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelDeal()
        {
            _selectView = DealsViews.Deal;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveDeal()
        {
            _selectView = DealsViews.Deal;
            _partialView = PartialViews.Details;
            await LoadData();
            StateHasChanged();
        }

        


    
        #region Attachments

        private void DetailsAttachment(int id)
        {
            _selectView = DealsViews.Attachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            _selectView = DealsViews.Attachments;
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
            _selectView = DealsViews.Attachments;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

       

       
        private void CloseAttachment()
        {
            _selectView = DealsViews.Attachments;

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
            if (_selectView == DealsViews.Deal)
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
                NavigationManager.NavigateTo($"/Deals");
        }

        private void DetailsTicket(int id)
        {
            NavigationManager.NavigateTo($"/Tickets/{id}/Info");
        }

        private void EditTicket(int? id)
        {
            var url = id == null
                ? $"/Tickets/New?IdDeal={Id}"
                : $"/Tickets/{id}/Edit";

            NavigationManager.NavigateTo(url);
        }

    }
}
