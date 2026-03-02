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
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Contacts
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }
        

        [Inject]
        IContactsService Service { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> LocalizeBtn { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public int? CompanyId { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }
       
        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private ContactDTO _contact = null;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
  
            try
            {
                
                _contact = await Service.GetItemAsync(Id);

                //_pageHeader = HeaderService.Create(ConstHelper.ClientContactsPath,Id, _contact?.NameComplete, false, ConstHelper.ClientContactsPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

      
        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Contacts/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/Contacts/Index");
        }


    }
}
