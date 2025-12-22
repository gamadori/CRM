using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared
{
    public partial class ChatTable: ComponentBase
    {
        [Parameter]
        public List<TicketChatViewModel> TicketChats { get; set; }

        [Parameter]
        public int TotalSize { get; set; }

        [Parameter]
        public EventCallback<PagingParameterModel> OnScroll { get; set; }

        private async ValueTask<ItemsProviderResult<TicketChatViewModel>> LoadChats(ItemsProviderRequest request)
        {
            var productNum = Math.Min(request.Count, TotalSize - request.StartIndex);

            await OnScroll.InvokeAsync(new PagingParameterModel
            {
                Skip = request.StartIndex,
                Top = productNum == 0 ? request.Count : productNum
            });

            return new ItemsProviderResult<TicketChatViewModel>(TicketChats, TotalSize);
        }

        protected override async Task OnInitializedAsync()
        {
           
            await base.OnInitializedAsync();
        }
    }
}
