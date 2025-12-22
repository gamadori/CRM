using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace CRM.Client.Shared.Components
{
    public partial class UserDropDown: ComponentBase
    {
        [Inject]
        IUserService UserService { get; set; }

        [Parameter]
        public string Value
        {
            get { return _idUser; }
            set
            {
                if (_idUser != value)
                {
                    _idUser = value;
                    ValueChanged.InvokeAsync(_idUser);
                }
            }
        }

        [Parameter]
        public EventCallback<string> ValueChanged { get; set; }

        [Parameter]
        public EventCallback<object> Changed { get; set; }


        private List<ApplicationUser> _users = null;

        private string? _idUser = null;
        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
        }

        private async Task LoadData()
        {
            if (UserService != null) 
            {
                _users = await UserService.Get<ApplicationUser>(ConstHelper.UsersPath);

                StateHasChanged();
            }

        }

        private async Task OnSelectedUserChange(object value)
        {

            if (Changed.HasDelegate)
                await Changed.InvokeAsync(value);
        }

       
    }
}
