using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketChats
{
    public partial class ChatPanel: ComponentBase
    {
        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public List<TicketChatViewModel> TicketChats { get; set; }

        [Parameter]
        public int TotalSize { get; set; }

        [Parameter]
        public EventCallback<TicketChatFilterModel> OnScroll { get; set; }

        private bool _firstTime = true;

        protected override async Task OnParametersSetAsync()
        {
           
            await base.OnParametersSetAsync();
        }

        
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _firstTime = true;

                
                await ScrollToBottom();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        public async Task Update()
        {
            _firstTime = true;

            await _chatContainer.RefreshDataAsync();
           
            

        }

        private Virtualize<TicketChatViewModel> _chatContainer;

      
        

        private async ValueTask<ItemsProviderResult<TicketChatViewModel>> LoadChats(ItemsProviderRequest request)
        {
           
            await OnScroll.InvokeAsync(new TicketChatFilterModel
            {
                Skip = request.StartIndex,
                Top = request.Count
            });
           

            var resp = new ItemsProviderResult<TicketChatViewModel>(TicketChats, TotalSize);

            return resp;
        }

        

        private async Task ScrollToTop()
        {

            await JSRuntime.InvokeVoidAsync(
                 "scrollToTop", "divChat");
        }

        private async Task ScrollToBottom()
        {

            await JSRuntime.InvokeVoidAsync(
                 "scrollToBottom", "divChat");
        }

        private async Task MsgLoaded(bool lstMsg)
        {
            if (_firstTime)
            {

                if (lstMsg)
                {
                    await _chatContainer.RefreshDataAsync();
                    await ScrollToBottom();
                    _firstTime = false;
                }
            }
        }
    }
}
