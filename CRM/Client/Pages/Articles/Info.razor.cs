using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Articles
{
    public partial class Info: ComponentBase
    {
        public enum ProductViews
        {
            Product,
            Ticket,
            Attachments,
            ProductAttachments,

            Backups
        }

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New,
            AddFiles

        }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        

        [Inject]
        IArticlesService Service { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }


        [Parameter]
        public Action OnGotoIndex { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public int? CompanyId { get; set; } = null;

        
        protected ProductViews selectView = ProductViews.Product;

        private PartialViews _partialView = PartialViews.Details;

        private int? _idTicket;

        private int? _idAttachment;

        private ArticleDTO _article = null;

        private bool _fromDetails = false;

        private PageHeaderModel? _pageHeader = null;

        private List<ViewOption<ProductViews>> _viewOptions;

        protected override async Task OnInitializedAsync()
        {
            InitializeViewOptions();
            await LoadProduct();
        }

        private void InitializeViewOptions()
        {
            _viewOptions = new List<ViewOption<ProductViews>>
            {
                new ViewOption<ProductViews> { Text = Localize["Data Product"], Value = ProductViews.Product },
                new ViewOption<ProductViews> { Text = Localize["Documenti Macchina"], Value = ProductViews.Attachments },
                new ViewOption<ProductViews> { Text = Localize["Documenti"], Value = ProductViews.ProductAttachments },
                new ViewOption<ProductViews> { Text = Localize["Tickets"], Value = ProductViews.Ticket },
            };
        }

        private async Task LoadProduct()
        {
            //_product = await _service.Get(Id);

            _article = await Service.GetItemAsync(Id);

            

            //_pageHeader = HeaderService.Create("Articles", Id, _article?.SerialNumber, false, ConstHelper.ClientArticlesPath, null);
            _pageHeader = await HeaderService.Create();
        }
        private void EditProduct()
        {
            selectView = ProductViews.Product;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }

        void GotoIndex()
        {
            if (OnGotoIndex != null)
            {
                OnGotoIndex();
            }
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}");
        }
        void CancelProduct()
        {
            selectView = ProductViews.Product;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }

        void IndexProduct()
        {
            selectView = ProductViews.Product;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }


        private async Task SaveProduct()
        {
            selectView = ProductViews.Product;
            _partialView = PartialViews.Details;
            await LoadProduct();
            StateHasChanged();
        }
        #region Ticket

        private void IndexTicket()
        {
            selectView = ProductViews.Ticket;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }

        private void EditTicket(int? id)
        {
            selectView = ProductViews.Ticket;
            _partialView = PartialViews.Edit;
            _idTicket = id;

            StateHasChanged();
        }

        private void IndexEditTicket(int? id)
        {
            _fromDetails = false;
            EditTicket(id);
        }

        private void DetailsTicket(int id)
        {
            //selectView = ProductViews.Ticket;
            //_partialView = PartialViews.Details;
            //_idTicket = id;
            //StateHasChanged();
            if (CompanyId.HasValue)
                NavigationManager.NavigateTo($"{ConstHelper.ClientCompaniesPath}/{CompanyId.Value}/{ConstHelper.ClientArticlesPath}/{Id}/{ConstHelper.ClientTicketsPath}/{id}");
            else
                NavigationManager.NavigateTo($"{ConstHelper.ClientArticlesPath}/{Id}/{ConstHelper.ClientTicketsPath}/{id}");
        }

        private void DetailsEditTicket()
        {
            _fromDetails = true;
            EditTicket(_idTicket);
        }

        private void TicketCloseForm()
        {
            selectView = ProductViews.Ticket;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }
        #endregion

        #region Attachments

        private void DetailsAttachment(int id)
        {
            selectView = ProductViews.Attachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }
        private void DetailsProductAttachment(int id)
        {
            selectView = ProductViews.ProductAttachments;
            _partialView = PartialViews.Details;
            _idAttachment = id;

            StateHasChanged();
        }

        private void EditAttachment(int? id)
        {
            selectView = ProductViews.Attachments;
            _partialView = PartialViews.Edit;
            _idAttachment = id;

            StateHasChanged();
        }


        private void IndexDetailsAttachment(int id)
        {
            _fromDetails = true;
            DetailsAttachment(id);
        }

        private void IndexProductDetailsAttachment(int id)
        {
            _fromDetails = true;
            DetailsProductAttachment(id);
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
            selectView = ProductViews.Attachments;
            _partialView = PartialViews.AddFiles;
            StateHasChanged();

        }




        private void CloseAttachment()
        {
            selectView = ProductViews.Attachments;

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

        private void CloseProductAttachment()
        {
            selectView = ProductViews.ProductAttachments;

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
            if (selectView == ProductViews.Product)
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
