using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Leads
{
    public partial class Edit : ComponentBase
    {
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private ILeadService LeadService { get; set; } = default!;
        [Inject] private ICompaniesService CompaniesService { get; set; } = default!;
        [Inject] private IProductsService ProductsService { get; set; } = default!;
        [Inject] private IEnumService EnumService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        [Parameter] public int? Id { get; set; }

        private PageHeaderModel? _pageHeader;
        private LeadDTO? _lead;
        private List<CompanyDTO> _companies = new();
        private List<ProductDTO> _products = new();
        private int? _selectedProductId;
        private bool _loading = true;
        private bool _saving;
        private string? _message;

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await LoadLead();
        }

        private async Task LoadLead()
        {
            _loading = true;
            try
            {
                _companies = await CompaniesService.GetListAsync(new CompanyFilter()) ?? new List<CompanyDTO>();
                _products = await ProductsService.GetListAsync(new ProductFilter()) ?? new List<ProductDTO>();
                _lead = Id == null
                    ? new LeadDTO { CreatedAt = DateTime.Now, Source = LeadSource.Manual, Status = LeadStatus.New, Score = 10 }
                    : await LeadService.GetItemAsync(Id.Value);

                _lead ??= new LeadDTO();
                SyncCompanyNameFromSelection();
                RecalculateLeadValue();
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task Save()
        {
            if (_lead == null) return;

            _saving = true;
            _message = null;
            try
            {
                var response = await LeadService.PostAsync(_lead.ToEntity()!);
                if (response.State)
                {
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientLeadsPath}");
                }
                else
                {
                    _message = response.Message;
                }
            }
            finally
            {
                _saving = false;
            }
        }

        private void Cancel() => NavigationManager.NavigateTo($"/{ConstHelper.ClientLeadsPath}");

        private string StatusText(LeadStatus status) => EnumService.Get(typeof(LeadStatus), status);

        private string SourceText(LeadSource source) => EnumService.Get(typeof(LeadSource), source);

        private int? SelectedCompanyId
        {
            get => _lead?.IdCompany;
            set
            {
                if (_lead == null) return;

                _lead.IdCompany = value;
                SyncCompanyNameFromSelection();
            }
        }

        private void SyncCompanyNameFromSelection()
        {
            if (_lead?.IdCompany == null) return;

            var company = _companies.FirstOrDefault(x => x.Id == _lead.IdCompany);
            if (company != null)
            {
                _lead.CompanyName = company.RagioneSociale;
            }
        }

        private string ProductText(ProductDTO product)
        {
            var code = string.IsNullOrWhiteSpace(product.Code) ? string.Empty : $"{product.Code} - ";
            return $"{code}{product.Name}";
        }

        private IEnumerable<ProductDTO> ProductsAvailableToAdd =>
            _products.Where(product => _lead?.ProductInterests.All(row => row.IdProduct != product.Id) != false);

        private void AddProductInterest()
        {
            if (_lead == null || _selectedProductId == null)
            {
                return;
            }

            var product = _products.FirstOrDefault(x => x.Id == _selectedProductId.Value);
            if (product == null || _lead.ProductInterests.Any(x => x.IdProduct == product.Id))
            {
                _selectedProductId = null;
                return;
            }

            var row = new ProductInterestDTO
            {
                IdProduct = product.Id,
                ProductCode = product.Code ?? string.Empty,
                ProductName = product.Name,
                Quantity = 1,
                UnitPrice = product.Price,
                SortOrder = _lead.ProductInterests.Count
            };
            RecalculateRow(row);
            _lead.ProductInterests.Add(row);
            _selectedProductId = null;
            RecalculateLeadValue();
        }

        private void RemoveProductInterest(ProductInterestDTO row)
        {
            if (_lead == null)
            {
                return;
            }

            _lead.ProductInterests.Remove(row);
            for (var index = 0; index < _lead.ProductInterests.Count; index++)
            {
                _lead.ProductInterests[index].SortOrder = index;
            }

            RecalculateLeadValue();
        }

        private void RecalculateProductInterest(ProductInterestDTO row)
        {
            RecalculateRow(row);
            RecalculateLeadValue();
        }

        private void RecalculateLeadValue()
        {
            if (_lead == null || _lead.ProductInterests.Count == 0)
            {
                return;
            }

            _lead.EstimatedValue = _lead.ProductInterests.Sum(x => x.LineTotal);
        }

        private static void RecalculateRow(ProductInterestDTO row)
        {
            row.Quantity = row.Quantity <= 0 ? 1 : row.Quantity;
            row.DiscountPct = Math.Clamp(row.DiscountPct, 0, 100);
            row.LineTotal = ProductInterestHelper.CalculateTotal(row.Quantity, row.UnitPrice, row.DiscountPct);
        }
    }
}
