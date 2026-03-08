using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Companies
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        [Inject]
        ICompaniesService Service { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> LocalizeBtn { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> LocalizeCompanyTypes { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public bool HeaderVisible {get; set;} = false;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public CompanyDTO? Company { get; set; }

        private CompanyDTO? _company = null;

        private PageHeaderModel? _pageHeader = null;

        private string? _logo = null;

        private bool _hasChildren = false;

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                
                path = ConstHelper.CompaniesPath;

                if (Id != null)
                {
                    if (Company != null)
                        _company = Company;
                    else
                        _company = await Service.GetItemAsync(Id.Value);
                }
                else
                    _company = new CompanyDTO();

                await GetLogo();
                await CheckHasChildren();
               
                _pageHeader = await HeaderService.Create(PageMode);
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task GetLogo()
        {
            _logo = await Service.GetLogo(Id.Value);
        }

        private async Task CheckHasChildren()
        {
            if (Id != null && _company != null &&
                (_company.CompanyType == CompanyTypes.HeadCompany || _company.CompanyType == CompanyTypes.Reseller))
            {
                var tree = await Service.GetTreeAsync(Id.Value);
                _hasChildren = tree != null && tree.Any(n => n.Children.Any());
            }
        }

        private async Task OpenTreeDialog()
        {
            await DialogService.OpenSideAsync<CompanyTree>(
                $"{Localize["Struttura Aziende"]} - {_company?.RagioneSociale}",
                new Dictionary<string, object>
                {
                    { "IdCompany", Id },
                    { "IsDialog", true }
                },
                new SideDialogOptions
                {
                    Position = DialogPosition.Top,
                    ShowMask = true,
                    Height = "auto",
                    Style = "max-height: 90%;"
                });
        }

        protected void EditCompany()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Companies/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Companies/Index");
        }

        protected void SendInvitation()
        {
        }
    }
}
