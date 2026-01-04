using BlazoringComponents;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;
using Microsoft.JSInterop;
using QLNet;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Settings.Users
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        [Inject]
        HttpClient Http { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IUserService _userService { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }


        [Parameter]
        public string Id { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public Action CloseForm { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private UserModel _user = null;

        private Dictionary<string, object> _attrubute;

        private bool readOnly = false;

        private List<CompanyDTO> _companies = new List<CompanyDTO>();

        private string _message;

        private string _header = "User";

        private string _labelYes = null;

        private string _labelNo = null;

        private Func<Task> msgBoxEvent;

        private MsgBoxConfirm msgBox;

        private bool _hidden = false;

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {

                DisplayComponent();

                

                _attrubute = new Dictionary<string, object>();

                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.UsersPath;


                if (Id != null)
                {
                    path += $"/Profile/{Id}";

                    _user = await Http.GetFromJsonAsync<UserModel>(path);
                }
                else
                {
                    _user = new UserModel() {IdCompany = IdCompany };

                }
                
                //_pageHeader = HeaderService.Create("Users", Id, _user?.UserName, true, ConstHelper.ClientUsersPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);
                await LoadCompanies(new LoadDataArgs());

                

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            

            try
            {
                //var resp = await _userService.Post(_user);


                if (Id != null)
                {
                    var resp = await Http.PutAsJsonAsync<UserModel>($"{ConstHelper.UsersPath}/{_user.Id}", _user);

                    if (resp.IsSuccessStatusCode)
                    {
                        await CloseAsync(); ;
                    }
                    else
                    {
                        _message = Localize["Errore durante il salvataggio"];
                    }

                }
                else
                {
                   

                    var resp = await Http.PostAsJsonAsync<UserModel>(ConstHelper.UsersPath, _user);

                    if (resp.IsSuccessStatusCode)
                    {
                        _message = "";
                        _user = await resp.ReadAsync<UserModel>();
                       
                        Id = _user?.Id;
                        await PrepareSendInvite();
                    }
                    else if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var apiResponse =  JsonSerializer.Deserialize<ApiResponseModel>(await resp.Content.ReadAsStringAsync(),
                            new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                        Notify(Localize[apiResponse.Message], NotificationSeverity.Error);
                    }
                    else
                    {
                        Notify(Localize[Localize["Errore durante il salvataggio"]], NotificationSeverity.Error);
                       
                    }
                }

                
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {

            if (CloseForm != null)
                CloseForm();
            else
                NavigationManager.NavigateTo($"/Settings/Users/Index");
        }

        protected void Delete()
        {

        }

        protected async Task LoadCompanies(LoadDataArgs args = null)
        {
            CompanyFilter data = new CompanyFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                data.RagioneSociale = args.Filter;
            }
            var resp = await RestClientService.GetListPag<CompanyFilter, CompanyDTO>(data, ConstHelper.CompaniesPath);

            _companies = resp?.Items;

            //StateHasChanged();
        }

        private async Task PrepareSendInvite()
        {
            _labelNo = "No";
            _labelYes = "Si";
            _message = $"Inviare l'invito all'utente creato {_user.NameComplete}";

            msgBoxEvent = SendInvite;
            StateHasChanged();
            
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");

            
        }

        private async Task SendInvite()
        {
            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgDelete");

            if (!await _userService.SendInvite(_user.Id))
            {
                _message = "Si è verificato un errore durante l'invio dell'invito";
            }
            else
                _message = "Invito inviato correttamente";

            _labelNo = null;
            _labelYes = "OK";
            msgBoxEvent = CloseAsync;
            
            StateHasChanged();

            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");


        }

        private async Task CloseAsync()
        {


            if (CloseForm != null)
                CloseForm();
            else
            {
                await JSRuntime.InvokeVoidAsync("CloseModal", "dlgDelete");
                NavigationManager.NavigateTo("/Settings/Users/Index");
            }
        }

        private void DisplayComponent()
        {
            if (PageMode == PageModality.Dialog)
                _hidden = true;
            else
                _hidden = false;

            StateHasChanged();
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

    }
}
