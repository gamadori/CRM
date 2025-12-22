using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static CRM.Client.Program;

namespace CRM.Client.Pages.TicketChats
{
    public partial class Index: ComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {


        [Inject]
        IJSRuntime JSRuntime { get; set; }
        
        [Inject]
        HttpClient HttpClient { get; set; }


        [Inject]
        ITicketChatsService Service { get; set; }
        
        [Inject] 
        NavigationManager Navigation { get; set; }

        [Inject] 
        AuthenticationStateProvider AuthenticationStateProvider { get; set; }

        
        [Parameter]
        public int IdTicket { get; set; }

        private List<TicketChatViewModel> ChatList { get; set; } = new List<TicketChatViewModel>();

        private int TotalSize { get; set; }

        private TicketChat _ticketChat = new TicketChat();

        private bool _firstTime = false;

        

        private string _msg;

        private TicketChatFilterModel _param = new TicketChatFilterModel();

        private Virtualize<TicketChatViewModel> _chatContainer;
        
        private System.Threading.Timer _timer;

        private async Task GetMessages()
        {
            
            _param.IdTicket = IdTicket;
            
            
            //var virtualizeResult = await RestClientHelper.Get<TicketChatViewModel>(HttpClient, $"{ConstHelper.TicketChatsPath}", param);
            //ChatList = virtualizeResult.Items;
            //TotalSize = virtualizeResult.MetaData.TotalCount;

            var paging = await Service.GetList(_param);

            if (paging != null)
                ChatList = paging.Items;

            StateHasChanged();
            await ScrollToBottom();
        }

        protected override async Task OnInitializedAsync()
        {

            DynamicNotificationHandlers.Register(this);

            await GetMessages();

            _timer = new System.Threading.Timer(CheckNewMessage, new System.Threading.AutoResetEvent(false), 10000, 10000);


        }
        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            await GetMessages();
            StateHasChanged();

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



        private async void CheckNewMessage(object stateInfo)
        {
            var resp = await Service.HasNewMessage(IdTicket);

            if (resp == true)
            {
                await GetMessages();
            }
        }

        protected async Task HandleValidSubmit()
        {


            try
            {
                if (_ticketChat != null && _ticketChat.Message != null && _ticketChat.Message.Length > 0)
                {
                    _ticketChat.Date = DateTime.Now;
                    _ticketChat.IdTicket = IdTicket;

                    var resp = await Service.Post(_ticketChat);

                    _ticketChat.Message = "";

                    await GetMessages();

                    

                    await InvokeAsync(StateHasChanged);

                    await ScrollToBottom();
                  
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task MsgLoaded(bool lstMsg)
        {
            if (_firstTime)
            {

                if (lstMsg)
                {
                   
                    await ScrollToBottom();
                    _firstTime = false;
                }
            }
        }

        private async Task ScrollToBottom()
        {
            //await grdTicketChat.LastPage();

            await JSRuntime.InvokeVoidAsync(
                 "scrollToBottom", "divChat");
        }

        private async Task ScrollToTop()
        {
            //await grdTicketChat.LastPage();

            await JSRuntime.InvokeVoidAsync(
                 "scrollToTop", "divChat");
        }

       

        public void Dispose()
        {
            _timer.Dispose();
            DynamicNotificationHandlers.Unregister(this);
        }
        
        private async void OnReceiveMessage(object sender, int idChat)
        {
            await GetMessages();
            
        }

       
    }
}

