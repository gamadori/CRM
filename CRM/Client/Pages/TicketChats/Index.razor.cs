using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
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

        private IBrowserFile? _pendingFile;
        private int? _pendingAttachmentFileId;
        private bool _uploading;

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
            await InvokeAsync(async () =>
            {
                await GetMessages();
            });
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
                await InvokeAsync(async () =>
                {
                    await GetMessages();
                });
            }
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            _pendingFile = e.File;
            _pendingAttachmentFileId = null;
            _uploading = true;
            StateHasChanged();

            var result = await Service.UploadFile(IdTicket, _pendingFile);
            _uploading = false;

            if (result != null)
                _pendingAttachmentFileId = result.Id;
            else
                _pendingFile = null;

            StateHasChanged();
        }

        private void ClearPendingFile()
        {
            _pendingFile = null;
            _pendingAttachmentFileId = null;
        }

        protected async Task HandleValidSubmit()
        {
            if (_uploading) return;

            var hasMessage = !string.IsNullOrWhiteSpace(_ticketChat?.Message);
            var hasFile = _pendingAttachmentFileId.HasValue;

            if (!hasMessage && !hasFile) return;

            try
            {
                _ticketChat.Date = DateTime.Now;
                _ticketChat.IdTicket = IdTicket;
                _ticketChat.IdAttachmentFile = _pendingAttachmentFileId;

                await Service.Post(_ticketChat);

                _ticketChat.Message = "";
                _pendingFile = null;
                _pendingAttachmentFileId = null;

                await GetMessages();
                await InvokeAsync(StateHasChanged);
                await ScrollToBottom();
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

