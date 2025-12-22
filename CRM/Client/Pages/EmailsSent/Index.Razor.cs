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

namespace CRM.Client.Pages.EmailsSent
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject] 
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IBaseRestService<EmailSent, EmailSentFilterModel, int> _serviceEmail { get; set; }

        [Inject]
        IRestService<ApplicationUser> userSigned { get; set; }

        [Parameter]
        public EventCallback<int> OnDetails { get; set; }


        private IQueryable<EmailSent> _emails = null;


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private EmailSentFilterModel _filter = new EmailSentFilterModel() {  PageSize = 10, Skip = 0, Top = 10 };

        private string _pageMessge = "";

        private string _messageDelete = "";

        private ApplicationUser? _user;

        private EmailSent _email;

        private string pagingSummaryFormat;

        private int _companyPageSize = 10;

        private bool _isLoading = false;

        private RadzenDataGrid<EmailSent> grdEmail;

        
        protected override async Task OnInitializedAsync()
        {
            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            _user = await userSigned.Get();
            await LoadData();
           
        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            try
            {
                await GetEmails(args);
            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;
            }
            finally
            {
                if (_emails == null)
                    _emails = Enumerable.Empty<EmailSent>().AsQueryable();
                
                
            }
     
        }

       

      

        public async Task GetEmails(LoadDataArgs args = null)
        {
            try                
            {

               

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;

               
                }
                
                PagingResponse<EmailSent> pagingResponse  = await _serviceEmail.Get(_filter);

                if (pagingResponse != null)
                {
                    _emails = pagingResponse.Items.AsQueryable();
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


        protected async Task Details(int id)
        {
            if (OnDetails.HasDelegate)
            {
                await OnDetails.InvokeAsync(id);
            }
            else
                NavigationManager.NavigateTo($"/EmailsSent/Details/{id}");
        }

       

       
    }
}
