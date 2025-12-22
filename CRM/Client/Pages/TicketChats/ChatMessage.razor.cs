using CRM.Client.Services;

using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketChats
{
    public partial class ChatMessage: ComponentBase
    {
        [Inject]
        ITicketChatsService Service { get; set; }

        [Parameter]
        public TicketChatViewModel TicketChat { get; set; }

        [Parameter]
        public EventCallback<bool> Loaded { get; set; }

        private string _color = "black";

        private string _bgClass = "alert-success";

        private string _align = "ms-auto";

        protected override async Task OnInitializedAsync()
        {
            //if (TicketChat != null)
            //{
            //    if (TicketChat.TypeMessage == TypeMessage.Receved)
            //    {


            //        _color = TicketChat.Color;
            //        _bgClass = "alert-light";
            //        _align = "me-auto";
            //    }
            //    else if (TicketChat.TypeMessage == TypeMessage.Sended)
            //    {
            //        _color = "black";
            //        _bgClass = "alert-success";
            //        _align = "ms-auto";
            //    }
            //    else
            //    {
            //        _color = "black";
            //        _bgClass = "alert-warning";
            //        _align = "mse-auto";
            //    }

            //}
            await Service.ChatRead(TicketChat.Id, TicketChat);
            StateHasChanged();
            await base.OnInitializedAsync();
        }

        
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            

            if (firstRender)
            {
               
                if (Loaded.HasDelegate)
                    await Loaded.InvokeAsync(TicketChat.Last);
            }
            await base.OnAfterRenderAsync(firstRender);
        }
    }

    
}
