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

namespace CRM.Client.Pages.Articles
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action<int>OnClickEditChild { get; set; } 

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public int? CompanyId { get; set; } = null;


        private Article _article = null;

        private PageHeaderModel _pageHeader = new PageHeaderModel();
        

        protected override async Task OnInitializedAsync()
        {
  
            try
            {
                await LoadArticle();
                //_pageHeader = HeaderService.Create("Articles", Id, _article?.SerialNumber, false, ConstHelper.ClientArticlesPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task LoadArticle()
        {
            _article = await RestClientService.GetItem<Article, int>(Id, ConstHelper.ArticlesPath);
        }

        private void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/Index");
        }
        private async Task HandleStateChanged()
        {
            // Ricarica l'articolo se necessario (es. se lo stato influenza altri campi)
            await LoadArticle();
            StateHasChanged();
        }

    }
}
