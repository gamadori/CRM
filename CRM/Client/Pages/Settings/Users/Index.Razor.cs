using CRM.Client.Helpers;
using CRM.Client.Pages.Groups;
using CRM.Client.Services;
using CRM.Shared;
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
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Settings.Users
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        

        [Inject]
        IUserService UserService { get; set; }


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject] 
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }

        [Parameter]
        public bool HeaderVisible { get; set; } = false;
        
        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public int? IdGroup { get; set; }

        [Parameter]
        public int? IdTicketType { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public Action<string> OnClickDetails { get; set; }
        [Parameter]
        public EventCallback<string> OnClickEdit { get; set; }

        [Parameter]
        public EventCallback<string> OnClickDelete { get; set; }

        [Parameter]
        public string MessagePrepareDelete { get; set; }

        [Parameter]
        public bool CmdDetails { get; set; } = true;

        [Parameter]
        public bool CmdEdit { get; set; } = true;

        [Parameter]
        public bool CmdDelete { get; set; } = true;

        [Parameter]
        public bool NeedConfirm { get; set; } = false;

        [Parameter]
        public bool BreadCrumbVisible { get; set; } = true;

        [Parameter]
        public int? IdProject { get; set; } = null;

        [Parameter]
        public int? IdProjectParent { get; set; } = null;

        [Parameter]
        public EventCallback<string?> OnSelectItem { get; set; }

        



        private IQueryable<ApplicationUser> _users = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private UsersFilterModel _filter = new UsersFilterModel();

        private string _pageMessge = "";

        private string _messageDelete = "";

        private int? _idUserToDelete = null;


       

        private string _header;

        private bool _isLoading = false;



        private RadzenDataGrid<ApplicationUser> grdUsers;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private FilterMode _filterMode = FilterMode.Advanced;

        protected override async Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif


            //_filter.AdminConfirmed = !NeedConfirm;


            //if (NeedConfirm)
            //    _header = Localize["Lista Utenti da Confermare"];
            //else
            //    _header = Localize["Lista Utenti"];

            //await LoadData();
            await InitPage();
            
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                if (!NeedConfirm)
                {
                    //var column = this.grdUsers.ColumnsCollection.FirstOrDefault(x => x.Property == "Enabled");

                    //if (column != null)
                    //{
                    //    column.FilterValue = true;
                    //    await this.grdUsers.Reload();
                    //}
                }
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        private void CreateBreadCrumb()
        {
            _bread.Clear();
            _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "Settings" });

            if (!NeedConfirm)
                _bread.Add(new BreadcrumbModel() { Title = Localize["Utenti"], Url = null });
            else
            {
                _bread.Add(new BreadcrumbModel() { Title = Localize["Utenti"], Url = "Settings/Users", Action = InitPage, Data = false  });
                _bread.Add(new BreadcrumbModel() { Title = Localize["Utenti da Confermare"], Url = null });
            }

        }

        public async Task InitPage(object? confirm = null)
        {
            if (confirm != null)
                NeedConfirm = (bool)confirm;

            _filter.AdminConfirmed = !NeedConfirm;

            if (NeedConfirm)
                _header = Localize["Lista Utenti da Confermare"];
            else
                _header = Localize["Lista Utenti"];

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();

            CreateBreadCrumb();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            await InvokeAsync(StateHasChanged);

            try
            {
                if (IdCompany != null)
                    _filter.IdCompany = IdCompany;

                if (IdGroup != null)
                    _filter.IdGroup = IdGroup;

                if (IdTicketType != null)
                    _filter.IdTicketType = IdTicketType;

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                _filter.PageSize = ConstHelper.PageSize;;
                _filter.IdProject = IdProject;
                _filter.IdProjectParent = IdProjectParent;

                var pagingResponse = await UserService.Get<ApplicationUser, UsersFilterModel>(_filter, ConstHelper.UsersPath);

                _users = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;

            }

            catch (Exception ex)
            {
               
                _pageMessge = ex.Message;
            }
            finally
            {
                _isLoading = false;
                if (_users == null)
                    _users = Enumerable.Empty<ApplicationUser>().AsQueryable();
                await InvokeAsync(StateHasChanged);
            }
     
        }



        protected async Task SearchSubmit()
        {
            if (_filter != null)
                await LoadData();
        }


        protected async Task Delete(ApplicationUser user)
        {
           
           

            if (user != null)
            {
                if (OnClickDelete.HasDelegate)
                {
                    await OnClickDelete.InvokeAsync(user.Id);
                    await LoadData();
                }
                else
                {
                    if (await DialogService.Confirm($"{Localize["Eliminare definitivamente l'utente:"]} {user.Name}", Localize["Delete"]) == true)
                    {
                        var resp = await UserService.Delete<string>(user.Id, ConstHelper.UsersPath);

                        if (!resp)
                        {
                            if (await DialogService.Confirm($"{Localize["Impossibile eliminare l'utente è assegnato a dei tickets. Vuoi disabilitarlo?"]}", Localize["Attenzione"]) == true)
                            {
                                await Disable(user.Id);
                            }
                        }
                        await LoadData();
                    }
                }
            }
        }

        

        protected async Task Disable(string id)
        {
            await UserService.Disable(id);
        }

        protected async void Edit(string idUser)
        {
            if (OnClickEdit.HasDelegate)
            {
                await OnClickEdit.InvokeAsync(idUser);
                await LoadData();
            }
            else
            {             
                NavigationManager.NavigateTo($"/Settings/Users/Edit/{idUser}");
            }
        }

        protected void Details(string idUser)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idUser);
            }
            else
                NavigationManager.NavigateTo($"/Settings/Users/Details/{idUser}");
        }


        protected async void NewItem()
        {
            if (OnClickEdit.HasDelegate)
            {
                await OnClickEdit.InvokeAsync();
                await LoadData();
            }
            else
                NavigationManager.NavigateTo($"/Settings/Users/Edit");
        }

        protected async Task OnChangeEmailFilter(ChangeEventArgs args)
        {
            _filter.Email = args.Value.ToString();
            await LoadData();
        }

        protected async Task OnChangeNameFilter(ChangeEventArgs args)
        {
            _filter.Name = args.Value.ToString();
            await LoadData();
        }

        protected async Task OnChangeSurnameFilter(ChangeEventArgs args)
        {
            _filter.SurName = args.Value.ToString();
            await LoadData();
        }

        protected async Task OnChangeAdminConfirmFilter(ChangeEventArgs args)
        {
            _filter.AdminConfirmed = (bool?)args.Value;
            await LoadData();
        }
        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.Email = "";
                _filter.SurName = "";
                _filter.Name = "";
                _filter.AdminConfirmed = null;

                await LoadData();
            }
            else
                await LoadData();

            StateHasChanged();
        }

        protected async Task Confirm(ApplicationUser user)
        {
            if (await DialogService.Confirm($"{Localize["Confermare l'utente"]} {user.NameComplete}", Localize["Conferma Utente"]))
            {
                await UserService.Confirm(user.Id);

                await grdUsers.Reload();
            }
        }

        private async Task OnClickName(string? id)
        {
            switch (PageMode)
            {
                case PageModality.Dialog:

                    if (OnSelectItem.HasDelegate)
                    {
                        await OnSelectItem.InvokeAsync(id);
                    }
                    else
                        DialogService.DialogService.CloseSide(id);
                    break;
                case PageModality.Visualization:
                    NavigationManager.NavigateTo($"/Settings/Users/Details/{id}");
                    break;
            }

        }

       
    }
}
