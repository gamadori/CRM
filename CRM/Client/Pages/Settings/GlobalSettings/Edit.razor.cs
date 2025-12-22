using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
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

namespace CRM.Client.Pages.Settings.GlobalSettings
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
       

        [Inject]
        IAGRestClientService RestClientServer { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private HttpClient _http { get; set; }

        private bool _saving = false;
      
        private GlobalSetting _settings = null;

        private IList<Company> _companies;

        private int _pageSize = 10;

        private int _companyCount = 0;
        
        private List<Logo> _loghi;

        protected override async Task OnInitializedAsync()
        {
            
            try
            {

                _settings = await RestClientServer.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);
                await LoadLoghi();
                await LoadCompany();
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



            var response = await RestClientServer.GetListPag<CompanyFilter, Company>(request, ConstHelper.CompaniesPath); 

            if (response != null)
            {
                _companyCount = response.MetaData.TotalCount;
                _companies = response.Items;
            }
        }

        private async Task LoadLoghi()
        {
            var loghi = await RestClientServer.Get<Logo, LogosFilterModel>(new LogosFilterModel(), ConstHelper.LogosPath);
            _loghi = loghi.Items;

        }


        protected async Task HandleValidSubmit()
        {
            

            try
            {
                _settings.ScheduleTimeStart = DateTime.Today + _settings.ScheduleTimeStart.TimeOfDay;
                _settings.ScheduleTimeEnd = DateTime.Today + _settings.ScheduleTimeEnd.TimeOfDay;
                _saving = true;
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
