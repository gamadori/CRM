using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Companies
{
    [Authorize]
    public partial class CompanyTree : ComponentBase
    {
        [Inject]
        ICompaniesService Service { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> LocalizeCompanyTypes { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public bool IsDialog { get; set; } = false;

        private List<CompanyTreeNodeDTO>? _tree;
        private PageHeaderModel? _pageHeader;
        private bool _isLoading = true;
        private bool _isDialog = false;

        protected override async Task OnParametersSetAsync()
        {
            _isDialog = IsDialog;
            _isLoading = true;
            try
            {
                _tree = await Service.GetTreeAsync(IdCompany);

                if (!_isDialog)
                    _pageHeader = await HeaderService.Create();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _tree = new List<CompanyTreeNodeDTO>();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void NavigateToCompany(int id)
        {
            if (_isDialog)
                DialogService.CloseSide();

            NavigationManager.NavigateTo($"/Companies/{id}");
        }

        private string GetIcon(CompanyTypes type) => type switch
        {
            CompanyTypes.HeadCompany => "domain",
            CompanyTypes.Reseller => "store",
            CompanyTypes.Customer => "business",
            _ => "business"
        };

        private string GetBadgeClass(CompanyTypes type) => type switch
        {
            CompanyTypes.HeadCompany => "bg-primary",
            CompanyTypes.Reseller => "bg-success",
            CompanyTypes.Customer => "bg-secondary",
            _ => "bg-secondary"
        };
    }
}
