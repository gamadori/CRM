using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazoringComponents;

namespace CRM.Client.Pages.Settings.Logos
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        
        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        private IQueryable<Logo> _loghi = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private LogosFilterModel _filter = new LogosFilterModel();

        private string _messageDelete = "";


        private Logo _logo;

        


        protected override async Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif


            await LoadData();

        }

        public async Task<IEnumerable<Logo>> LoadData()
        {
            var loghi = Enumerable.Empty<Logo>().AsQueryable();
            try
            {

                var pagingResponse = await RestClientService.Get<Logo, LogosFilterModel>(_filter, ConstHelper.LogosPath);    

                _loghi = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;

                loghi = _loghi;
                
                return loghi;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex);
                return loghi;
            }
            finally
            {
                
            }
     
        }

      

        protected async Task SearchSubmit()
        {
            if (_filter != null)
                await LoadData();
        }

        protected void NewLogo()
        {
            NavigationManager.NavigateTo("/Settings/Logos/Edit");
        }

        protected async Task Delete()
        {
           
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            if (_logo != null)
            {
                await RestClientService.Delete<int>(_logo.Id, ConstHelper.LogosPath);               

                await LoadData();
            }
        }

        protected void PrepareDelete(Logo item)
        {
            _logo = item;
            _messageDelete = $"Eliminare definitivamente il logo: {_logo.Codice}";
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");

        }
    }
}
