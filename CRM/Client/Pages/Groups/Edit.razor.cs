using CRM.Client.Helpers;
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

namespace CRM.Client.Pages.Groups
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private Group _group = null;

        private string _messageState = null;

        private string _header = "Gruppo";

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "Settings" });
                _bread.Add(new BreadcrumbModel() { Title = Localize["Gruppi"], Url = "Settings/Groups" });

                
                if (Id != null)
                {
                    
                    _header = Localize["Modifica Gruppo"];
                    _group = await RestClientService.GetItem<Group, int>(Id.Value, ConstHelper.GroupsPath);

                    _bread.Add(new BreadcrumbModel() { Title = _group.Name, Url = $"Settings/Groups/Details/{Id}" });
                    _bread.Add(new BreadcrumbModel() { Title = Localize["Modifica Gruppo"], Url = null });
                }
                else
                {
                    _bread.Add(new BreadcrumbModel() { Title = Localize["Nuovo Gruppo"], Url = null });
                    _header = Localize["Nuovo Gruppo"];
                    _group = new Group();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await RestClientService.Post<Group, GroupFilter>(_group, ConstHelper.GroupsPath);

                if (resp != null && resp.State)
                {
                    if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo("/Settings/Groups");
                }
                else
                    _messageState = "Errore durante il salvataggio";
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Settings/Groups/Index");
        }

        void Change(string value, string name)
        {

        }

        void Error(Radzen.UploadErrorEventArgs args, string name)
        {

        }

    }
}
