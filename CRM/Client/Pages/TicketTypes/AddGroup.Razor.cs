using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketTypes
{
    public partial class AddGroup : ComponentBase
    {
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        private IManyToManyService<TicketTypeGroup> _service { get; set; }

        [Inject] 
        private Radzen.DialogService dialogService { get; set; }

        [Parameter]
        public int IdTicket { get; set; }

        [Parameter]
        public Action OnClickClose { get; set; }

        private List<Group> _groups { get; set; }

        private Group _group;

        private TicketTypeGroup _ticketTypeGroup = null;

        protected override async Task OnInitializedAsync()
        {
           
            await LoadGroups(new Radzen.LoadDataArgs());
            _ticketTypeGroup = new TicketTypeGroup() { IdTicket = IdTicket };
            _group = new Group();

        }
        public async Task LoadGroups(Radzen.LoadDataArgs args)
        {
            GroupFilter request = new GroupFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.Name = args.Filter;
            }
            var response = await RestClientService.Get<Group, GroupFilter>(request, ConstHelper.GroupsPath);

            _groups = response.Items.ToList();

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
           

            await _service.
                Post(_ticketTypeGroup);
            dialogService.Close();
            
        }

        protected void Cancel()
        {
            dialogService.Close();
        }
    }

   
    
}
