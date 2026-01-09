using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
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
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Companies
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> LocalizeBtn { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> LocalizeCompanyTypes { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

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

        private Company _company = null;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                
                path = ConstHelper.CompaniesPath;

                if (Id != null)
                {
                    path += $"/{Id}";

                    _company = await Http.GetFromJsonAsync<Company>(path);
                }
                else
                    _company = new Company();

                _pageHeader = await HeaderService.Create(PageMode);
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            HttpResponseMessage resp;

            try
            {
                if (_company != null && _company.Id > 0)
                    resp = await Http.PutAsJsonAsync<Company>($"{ConstHelper.CompaniesPath}/{_company.Id}", _company);
                else
                    resp = await Http.PostAsJsonAsync<Company>(ConstHelper.CompaniesPath, _company);

                NavigationManager.NavigateTo("/Companies/Index");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
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
