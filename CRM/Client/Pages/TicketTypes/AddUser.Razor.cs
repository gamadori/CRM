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

namespace CRM.Client.Pages.TicketTypes
{
    public partial class AddUser : ComponentBase
    {
        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _usersService { get; set; }

        [Inject]
        IManyToManyService<TicketTypeUser> _service { get; set; }

        [Inject] 
        DialogService dialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int IdTicket { get; set; }

        [Parameter]
        public Action OnClickClose { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Dialog;    

        private List<UserFiltered> _users { get; set; }

        private ApplicationUser _user;

        private TicketTypeUser _ticketTypeUser = null;

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
           
            await LoadUsers(new LoadDataArgs());
            _ticketTypeUser = new TicketTypeUser() { IdTicket = IdTicket };
            _user = new ApplicationUser();

            _pageHeader = await HeaderService.Create(PageMode);

        }
        public async Task LoadUsers(LoadDataArgs args)
        {
            UsersFilterModel request = new UsersFilterModel();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.Name = args.Filter;
            }
            var response = await _usersService.Get(request);

            _users = response.Items.Select(x=>new UserFiltered() { Id = x.Id, NameComplete = $"{x.Surname} {x.Name}"  }).ToList();

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
           

            await _service.Post(_ticketTypeUser);
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
