using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Companies
{
    [Authorize]
    public partial class Selector: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject] 
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }    

        [Parameter]
        public EventCallback<int> OnSelected { get; set; }

        [Parameter]
        public int IdCompany { get; set; }

        
        private IQueryable<Company> _companies = null;


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private CompanyFilter _filter = new CompanyFilter();

        private string _pageMessge = "";


 

        private string pagingSummaryFormat;

        private int _companyPageSize = 10;

        private bool _isLoading = false;

        private RadzenDataGrid<Company> grdCompanies;

        protected override async Task OnInitializedAsync()
        {
            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            await LoadData();

        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            try
            {
                await GetCompanies(args);
            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;
            }
            finally
            {
                if (_companies == null)
                    _companies = Enumerable.Empty<Company>().AsQueryable();
                
                
            }
     
        }

       

      

        public async Task GetCompanies(LoadDataArgs args = null)
        {
            try                
            {

                _filter.PageSize = _companyPageSize;

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;

               
                }

                PagingResponse<Company> pagingResponse = await RestClientService.Get<Company, CompanyFilter>(_filter, ConstHelper.CompaniesPath);

                if (pagingResponse != null)
                {
                    _companies = pagingResponse.Items.AsQueryable();
                    _paging = pagingResponse.MetaData;
                }
                else
                    _pageMessge = "Errore";

                

            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {
                
                _pageMessge = ex.Message;
                
            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;
                
            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }


      

        protected async void OnChangeRagioneSocialeFilter(ChangeEventArgs args)
        {
            _filter.RagioneSociale = args.Value.ToString();
            await grdCompanies.GoToPage(0);
            await LoadData();
        }

        protected async void OnChangeStatoFilter(ChangeEventArgs args)
        {
            _filter.Stato = args.Value.ToString();
            await grdCompanies.GoToPage(0);
            await LoadData();
        }

        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.RagioneSociale = "";
                _filter.Stato = "";

                
            }
            await LoadData();
        }

        protected void SelectCompany(int id)
        {
            if (OnSelected.HasDelegate)
                OnSelected.InvokeAsync(id);
        }
      
    }
}
