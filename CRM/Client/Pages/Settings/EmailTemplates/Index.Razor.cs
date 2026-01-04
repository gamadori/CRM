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
using Radzen.Blazor;
using CRM.Shared.Resources.Models;
using Radzen;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Localization;

namespace CRM.Client.Pages.Settings.EmailTemplates
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       

        [Inject]
        private NavigationManager NavigationManager { get; set; }

       

        [Inject]
        IAGRestClientService RestClientService { get; set; } 


        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private INavMenuService navMenuService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IEnumService EnumService { get; set; }

        private IQueryable<EmailTemplate> _templates = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private EmailTemplateFilter _filter = new EmailTemplateFilter();

   

        private bool _isLoading = false;

        private RadzenDataGrid<EmailTemplate> grdEmails;

        private string _header = "Email Templates";
        protected override async Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif

            //navMenuService.CallRequestRefresh();
            await LoadData();

        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            var template = Enumerable.Empty<EmailTemplate>().AsQueryable();
            try
            {

                _header = Localize["EmailTemplates"];

                

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await RestClientService.Get<EmailTemplate, EmailTemplateFilter>(_filter, ConstHelper.EmailTemplatePath);

                _templates = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;




            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }

        }


      

        protected async Task SearchSubmit()
        {
            if (_filter != null)
                await LoadData();
        }

        protected void NewTemplate()
        {
            NavigationManager.NavigateTo("/Settings/EmailTemplates/New");
        }

       

        private void Edit(int id)
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientEmailTemplatesPath}/{id}/Edit");
        }

        private async Task Delete(EmailTemplate item)
        {

            if (await DialogService.Confirm(string.Format(Localize["Elliminare il template {0}?"], item.Subject)) == true)
            {

                
                    await RestClientService.Delete<int>(item.Id, ConstHelper.EmailTemplatePath);


                    await LoadData();
                

            }
        }
    }
}
