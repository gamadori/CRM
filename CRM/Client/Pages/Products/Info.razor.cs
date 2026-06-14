using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Pages.Projects;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Products
{
    public partial class Info: ComponentBase
    {

        public enum GroupViews
        {
            ProductType,
            ProductAccessories,
            ProductDxf,
            ProductTypeChilds,
            Attachments,
            Backups,
            Catalog
        }

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New,
            Translate,
            AddFiles,
            View

        }

        //[Inject]
        //private IBaseRestService<Product, ProductFilter, int> service { get; set; }

        [Inject]
        IProductsService ProductsService { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private DialogService dialogService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Inject]
        private IManyToManyService<ProductParentChildModel> _parentChildService { get; set; }

        [Inject]
        private IHeaderService HeaderService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        protected GroupViews _selectView = GroupViews.ProductType;

        private AddChild _addChild;

        private PartialViews _partialView = PartialViews.Details;

        private int _idChild;

        private ProductDTO _product = null;

        private bool _fromDetails = false;

        private int _idAccessory;

        private int? _idAttachment;
        private string _messagePrepareDelete = "Eliminare la sotto parte {0} dal ";

        private List<ViewOption<GroupViews>> _viewOptions;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            InitializeViewOptions();
            if (NavigationManager.ToAbsoluteUri(NavigationManager.Uri).Query.Contains("view=backups", StringComparison.OrdinalIgnoreCase))
            {
                _selectView = GroupViews.Backups;
                _partialView = PartialViews.Index;
            }
            await LoadProduct();

            //_pageHeader = HeaderService.Create("Products", Id, _product?.Name, false, ConstHelper.ClientProductsPath, null);
            _pageHeader = await HeaderService.Create(PageMode);
        }

        private void InitializeViewOptions()
        {
            _viewOptions = new List<ViewOption<GroupViews>>
            {
                new ViewOption<GroupViews> { Text = Localize["Data Product"], Value = GroupViews.ProductType },
                new ViewOption<GroupViews> { Text = "Catalogo", Value = GroupViews.Catalog },
                new ViewOption<GroupViews> { Text = "Backup", Value = GroupViews.Backups },
                new ViewOption<GroupViews> { Text = Localize["Attachments"], Value = GroupViews.Attachments },
                //new ViewOption<GroupViews> { Text = Localize["Accessories"], Value = GroupViews.ProductAccessories },
                //new ViewOption<GroupViews> { Text = Localize["Sotto Parti"], Value = GroupViews.ProductTypeChilds }
            };
        }

        
        private async Task LoadProduct()
        {
            _product = await ProductsService.GetItemAsync(Id); // service.Get(Id);
          
        }
        private void EditCommand()
        {
            _selectView = GroupViews.ProductType;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }


        void CancelCommand()
        {
            _selectView = GroupViews.ProductType;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveCommand()
        {
            _selectView = GroupViews.ProductType;
            _partialView = PartialViews.Details;
            await LoadProduct();
            StateHasChanged();
        }
        private void EditChild(int id)
        {
            _selectView = GroupViews.ProductTypeChilds;
            _partialView = PartialViews.Edit;
            _idChild = id;

            StateHasChanged();
        }

        private void UsersEditChild(int id)
        {
            _fromDetails = false;
            EditChild(id);
        }

        private void DetailsEditChild(int id)
        {
            _fromDetails = true;
            EditChild(id);
        }

        private void DetailsChild(int id)
        {
            _selectView = GroupViews.ProductTypeChilds;
            _partialView = PartialViews.Details;
            _idChild = id;

            StateHasChanged();
        }

        private void UsersCloseForm()
        {
            _selectView = GroupViews.ProductTypeChilds;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }

        void Change(object value, string name)
        {

            _fromDetails = false;

            if (_selectView == GroupViews.ProductType)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            StateHasChanged();
        }

        protected void OnClickNew(int? id)
        {
            dialogService.Open<AddChild>($"Tipo Prodotto {_product.Name}",
                        new Dictionary<string, object>() { { "IdParent", Id } },
                        new DialogOptions() { Width = "700px", Height = "530px" });

            dialogService.OnClose += async (s) => await OnClickClose(s);
        }

        protected async Task OnClickDelete(int idChild)
        {

            await _parentChildService.Delete(new ProductParentChildModel() {IdParent  = Id, IdChild = idChild });
            dialogService.Close();
            await LoadProduct();
            StateHasChanged();
        }

        protected async Task OnClickClose(dynamic result)
        {
            //dialogService.Close();
            await LoadProduct();
            StateHasChanged();
        }

        private void OnClickAccessoryDetails(int id)
        {
            _idAccessory = id;
            _partialView = PartialViews.Details;

            StateHasChanged();

        }

        private void OnClickAccessoryEdit(int? id)
        {
            _idAccessory = (int)id;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        private void OnClickAccessoryEditFromDetails()
        {
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        private void OnClickAccessoryNew()
        {
            _partialView = PartialViews.New;
            StateHasChanged();
        }

        private void OnClickAccessoryClose()
        {
            _partialView = PartialViews.Index;
            StateHasChanged();
        }

        #region Attachments

        private void DetailsAttachment(int id)
        {
            _selectView = GroupViews.Attachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void EditAttachment(int? id)
        {
            _selectView = GroupViews.Attachments;
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
            _selectView = GroupViews.Attachments;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

        private void CloseAttachment()
        {
            _selectView = GroupViews.Attachments;

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
        #region Dxf

        private void DetailsDxf(int id)
        {
            _selectView = GroupViews.ProductDxf;
            _partialView = PartialViews.View;
            _idAttachment = id;

            StateHasChanged();
        }

        private void EditDxf(int? id)
        {
            _selectView = GroupViews.ProductDxf;
            _partialView = PartialViews.Edit;
            _idAttachment = id;

            StateHasChanged();
        }

        private void IndexDetailsDxf(int id)
        {
            _fromDetails = true;
            DetailsDxf(id);
        }
        private void IndexEditDxf(int? id)
        {
            _fromDetails = true;
            EditDxf(id);
        }

        private void DetailsEditDxf(int? id)
        {
            _fromDetails = true;
            EditDxf(id);
        }

        private void AddFileDxf()
        {
            _selectView = GroupViews.ProductDxf;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }

        private void CloseDxf()
        {
            _selectView = GroupViews.ProductDxf;

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






    }
}
