using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Groups
{
    public partial class AddUser : ComponentBase
    {
        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _userService { get; set; }

        [Inject]
        IManyToManyService<UserGroupModel> _userGroupService { get; set; }

        [Inject] 
        DialogService dialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }
        [Parameter]
        public int IdGroup { get; set; }

        [Parameter]
        public Action OnClickClose { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Dialog;

        private List<UserFiltered> _users { get; set; }

        private ApplicationUser _user;

        private UserGroupModel _userGroup = null;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
           
            await LoadUsers(new LoadDataArgs());
            _userGroup = new UserGroupModel() { IdGroup = IdGroup };
            _user = new ApplicationUser();
            _pageHeader = await HeaderService.Create(PageMode);
        }
        public async Task LoadUsers(LoadDataArgs args)
        {
            UsersFilterModel request = new UsersFilterModel();

            //if (args != null && !string.IsNullOrEmpty(args.Filter))
            //{
            //    request.NameComplete = args.Filter;
            //}
            var response = await _userService.GetList(request);

            _users = response.Items.Select(x=>new UserFiltered() { Id = x.Id, NameComplete = $"{x.Surname} {x.Name}"  }).ToList();

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
            await _userGroupService.Post(_userGroup);
            dialogService.Close();
            
        }

        protected void Cancel()
        {
            dialogService.Close();
        }
    }

    public class UserFiltered
    {
        public string Id { get; set; }

        public string NameComplete { get; set; }
    }

    
}
