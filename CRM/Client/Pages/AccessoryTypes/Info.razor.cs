using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.AccessoryTypes
{
    public enum PageViews
    {
        AccessoryType,
        Accessory,
        Translate
    }

    public partial class Info : ComponentBase
    {
        

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New,
            Null

        }

        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        private IAccessoryTypesService AccessoryTypesService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set;}

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int SelectView
        {
            get { return (int)_selectView; }
            set { _selectView = (PageViews)value; }
        }

        private PageViews _selectView  = PageViews.AccessoryType;

        private PartialViews _partialView = PartialViews.Details;

        
        private int? _idAccessory;



        private AccessoryType _accessoryType = null;

        private bool _fromDetails = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        bool singleValue = false;

        protected override async Task OnInitializedAsync()
        {
            
            await LoadAccessoryType();

            _bread = await BreadCrumbService.AccessoryTypes(_accessoryType?.Name, false);

            
            if (_selectView == PageViews.Accessory)
                _partialView = PartialViews.Index;
        }

        private async Task LoadAccessoryType()
        {
            _accessoryType = await AccessoryTypesService.Get(Id);
        }
        private void EditAccessoryType()
        {
            _fromDetails = false;
            _selectView = PageViews.AccessoryType;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        
        void CancelAccessoryType()
        {
            _selectView = PageViews.AccessoryType;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveAccessoryType()
        {
            _selectView = PageViews.AccessoryType;
            _partialView = PartialViews.Details;
            await LoadAccessoryType();
            StateHasChanged();
        }

        private  void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}");
        }


        #region Accessory

        private void AccessoriesIndex()
        {
            _selectView = PageViews.Accessory;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }

        private void AccessoryEdit(int? id)
        {
            _selectView = PageViews.Accessory;
            _partialView = PartialViews.Edit;
            _idAccessory = id;

            StateHasChanged();
        }

        private void AccessoryIndexEdit(int? id)
        {
            _fromDetails = false;
            AccessoryEdit(id);
        }

        private void AccessoryDetails(int id)
        {
            _selectView = PageViews.Accessory;
            _partialView = PartialViews.Details;
            _idAccessory = id;

            StateHasChanged();
        }


        private void AccessoryDetailsEdit()
        {
            _fromDetails = true;
            AccessoryEdit(_idAccessory);
        }

        private void AccessoryCloseForm()
        {
            _selectView = PageViews.Accessory;
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
            if (_selectView == PageViews.AccessoryType)
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
