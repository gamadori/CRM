using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Settings.Users
{
    [Authorize]
    public partial class Profile: ComponentBase
    {
        [Inject]
        private IRestService<ApplicationUser> userService { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IUserService _userService { get; set; }

       

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }
       
        [Inject]
        private HttpClient HttpClient { get; set; }

        
        [Inject] 
        private NavigationManager Nav { get; set; }


        private UserModel _user = null;

        private List<Language> _languages;

         
        protected override async Task OnInitializedAsync()
        {
            try
            {
                _languages = await HttpClient.GetFromJsonAsync<List<Language>>(ConstHelper.LanguagesPath);

                _user = await HttpClient.GetFromJsonAsync<UserModel>($"{ConstHelper.UserSignedPath}/Profile");
                
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

     

        protected void Annulla()
        {
            
            NavigationManager.NavigateTo("/");
        }

        protected void Edit()
        {
            
            NavigationManager.NavigateTo($"/Settings/Users/Edit/{_user.Id}");
        }

       

        protected void PhotoChange()
        {
            StateHasChanged();
        }
        protected async Task HandleValidSubmit()
        {
            HttpResponseMessage resp;
            try
            {
                resp = await HttpClient.PostAsJsonAsync<UserModel>($"{ConstHelper.UserSignedPath}/Profile", _user);

                Culture = new CultureInfo(_user.LanguageCode);
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        private CultureInfo Culture
        {
            get => CultureInfo.CurrentCulture;
            set
            {
                if (CultureInfo.CurrentCulture != value)
                {
                    var js = (IJSInProcessRuntime)JSRuntime;
                    js.InvokeVoid("appCulture.set", value.Name);
                    Nav.NavigateTo(Nav.Uri, forceLoad: true);
                }
            }
        }

    }
}
