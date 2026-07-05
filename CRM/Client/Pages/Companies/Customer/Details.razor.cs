using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Companies.Customer
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        ICompaniesService CompaniesService { get; set; }


        [Parameter]
        public int? Id { get; set; }


        private CompanyDTO _company = null;

        private PageHeaderModel _pageHeader = null;

        protected override void OnInitialized()
        {
            _pageHeader = new PageHeaderModel
            {
                Title = Localize["Azienda"],
                Icon = "business",
                BreadcrumbItems = new List<BreadcrumbItem>
                {
                    new BreadcrumbItem("Home", "DashBoardClient"),
                    new BreadcrumbItem(Localize["Company"], null)
                }
            };
            base.OnInitialized();
        }

        protected override async Task OnInitializedAsync()
        {
            try
            {

                _company = await CompaniesService.GetUserCompany(); 
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

    }
}
