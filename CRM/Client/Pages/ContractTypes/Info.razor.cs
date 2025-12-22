using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using QLNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ContractTypes
{
    public enum ContractViews
    {
        ContractType,
        TicketsTypes,
        Attachments
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
        IContractTypesService Service { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }
        

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int SelectView
        {
            get { return (int)_selectView; }
            set { _selectView = (ContractViews)value; }
        }

        private ContractViews _selectView  = ContractViews.ContractType;

        private PartialViews _partialView = PartialViews.Details;

        private ContractType _item = null;

        private bool _fromDetails = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private int _idContractType;

        private int? _idTicketType;

        private int? _idAttachment;

        bool singleValue = false;

        protected override async Task OnInitializedAsync()
        {
            
            await LoadContractType();
            _bread = await BreadCrumbService.ContractTypes(false, _item?.Name);

          

            if (_selectView == ContractViews.TicketsTypes)
                _partialView = PartialViews.Index;
        }

        private async Task LoadContractType()
        {
            _item = await Service.Get(Id);
        }
        private void EditContractType()
        {
            _fromDetails = false;
            _selectView = ContractViews.ContractType;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelContractType()
        {
            _selectView = ContractViews.ContractType;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveContractType()
        {
            _selectView = ContractViews.ContractType;
            _partialView = PartialViews.Details;
            await LoadContractType();
            StateHasChanged();
        }

        private  void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else
                NavigationManager.NavigateTo($"{ConstHelper.ClientContractTypesPath}");
        }


        #region Tickets

        private void TicketsIndex()
        {
            _selectView = ContractViews.TicketsTypes;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }

        private void TicketEdit(int? id)
        {
            _selectView = ContractViews.TicketsTypes;
            _partialView = PartialViews.Edit;
            _idTicketType = id;

            StateHasChanged();
        }

        private void TicketIndexEdit(int? id)
        {
            _fromDetails = false;
            TicketEdit(id);
        }

        private void TicketDetails(int id)
        {
            _selectView = ContractViews.TicketsTypes;
            _partialView = PartialViews.Details;
            _idTicketType = id;

            StateHasChanged();
        }


        private void TicketDetailsEdit()
        {
            _fromDetails = true;
            TicketEdit(_idTicketType);
        }

        private void TicketNew()
        {
            _partialView = PartialViews.New;
            StateHasChanged();
        }

        private void TicketCloseForm()
        {
            _selectView = ContractViews.TicketsTypes;
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
            _selectView = ContractViews.Attachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            _selectView = ContractViews.Attachments;
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
            _selectView = ContractViews.Attachments;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

       

       
        private void CloseAttachment()
        {
            _selectView = ContractViews.Attachments;

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
            if (_selectView == ContractViews.ContractType)
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
