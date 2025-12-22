using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Settings.Users
{
    [Authorize(Policy = "AdminRole")]
    public partial class Roles : ComponentBase
    {
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _service { get; set; }

       

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Parameter]
        public string Id { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }


        private ApplicationUser _user = new ApplicationUser();

        private List<UserRole> _roles = new List<UserRole>();

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        protected override async Task OnInitializedAsync()
        {
            await LoadData();

            _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "Settings" });
            _bread.Add(new BreadcrumbModel() { Title = Localize["Utenti"], Url = "Settings/Users" });
            _bread.Add(new BreadcrumbModel() { Title = _user.UserName, Url = $"Settings/Users/Details/{Id}" });
            _bread.Add(new BreadcrumbModel() { Title = Localize["Ruoli"], Url = null });
        }
        protected async Task LoadData()
        {
            bool selected;
            _user = await _service.Get(Id);
            _roles.Clear();

            var enumType = typeof(eRoles);
            foreach (eRoles value in Enum.GetValues(enumType))
            {
                selected = _user.Roles.Contains(value.ToString());
                _roles.Add(new UserRole() { IdRole = value, RoleName = UtilityHelper.GetDisplayName(value), Selected = selected });
            }
        }

        protected async Task HandleValidSubmit()
        {
            UserRoles model = new UserRoles() { Id = Id };

            foreach (var r in _roles)
            {
                if (r.Selected)
                {
                    model.Roles.Add(r.IdRole.ToString());
                }
            }
            await RestClientService.Post<UserRoles, string>(model, ConstHelper.UserRolesPath); 
            NavigationManager.NavigateTo($"/Settings/Users/Details/{Id}");
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo($"/Settings/Users/Details/{Id}");
        }
    }
}
