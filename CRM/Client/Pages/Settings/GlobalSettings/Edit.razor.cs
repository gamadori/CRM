using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Settings.GlobalSettings
{
    [Authorize]
    public partial class Edit: ComponentBase
    {

        [Inject]
        ICompaniesService CompaniesService { get; set; }

        [Inject]
        IAGRestClientService RestClientServer { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private bool _saving = false;
      
        private GlobalSetting _settings = null;

        private IList<CompanyDTO> _companies;

        private int _pageSize = 10;

        private int _companyCount = 0;
        
        private List<Logo> _loghi;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                
                await LoadLoghi();
                await LoadCompany();
                _settings = await RestClientServer.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);

                _pageHeader = await HeaderService.Create(PageMode);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (_settings == null)
                    _settings = new GlobalSetting();
            }
        }

        public async Task LoadCompany(LoadDataArgs args = null)
        {
            CompanyFilter request = new CompanyFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.RagioneSociale = args.Filter;
            }

            _companies = await CompaniesService.GetListAsync(request); await RestClientServer.GetListPag<CompanyFilter, Company>(request, ConstHelper.CompaniesPath); 

            
        }

       

        private async Task LoadLoghi()
        {
            var loghi = await RestClientServer.GetList<Logo, LogosFilterModel>(new LogosFilterModel(), ConstHelper.LogosPath);
            _loghi = loghi ?? new List<Logo>();

        }


        protected async Task HandleValidSubmit()
        {
            try
            { 
                StateHasChanged();

                var  resp = await RestClientServer.Post<GlobalSetting, int>(_settings, ConstHelper.GlobalSettingsPath); 
                                
                if (resp != null)
                {
                    if (resp.State)
                    {
                        NavigationManager.NavigateTo("/Settings");
                    }
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            finally
            {
                _saving = false;
            }
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo("/Settings");
        }
    }
}
