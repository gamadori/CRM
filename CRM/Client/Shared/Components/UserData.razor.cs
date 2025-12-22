using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class UserData : ComponentBase
    {
       
        [Inject]
        IUserService UserService { get; set; }
       
        [Parameter]
        public string? IdUser { get; set; }


        private ApplicationUser? _user = null;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            await LoadData();
            await base.OnParametersSetAsync();
        }
        private async Task LoadData()
        {
            if (UserService != null && IdUser != null && IdUser.Length > 0)
            {
                _user = await UserService.GetItem<ApplicationUser, string>(IdUser, ConstHelper.UsersPath);
            }
            else
                _user = new ApplicationUser();

            StateHasChanged();
        }
    }
}
