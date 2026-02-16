using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static CRM.Client.Program;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace CRM.Client.Pages.DashBoardClient
{
    [Authorize]
    public partial class Index : ComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        private ITicketFeedbackService FeedbackService { get; set; }

        [Inject]
        private DialogService DialogService { get; set; }

        TicketDashBoardModel _tickets;
        Ticket _ticket;
        
        // Feedback
        private List<TicketPendingFeedback> _pendingFeedbacks = new();
        private int _pendingFeedbackCount = 0;

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            await LoadData();
            await LoadPendingFeedbacks();

            await base.OnInitializedAsync();
        }

        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            await LoadData();
            StateHasChanged();
        }

        private async Task LoadData()
        {
            _tickets = await RestClientService.GetFirst<TicketDashBoardModel>(ConstHelper.TicketsDashboardPath);
        }

        private async Task LoadPendingFeedbacks()
        {
            try
            {
                _pendingFeedbacks = await FeedbackService.GetPendingFeedbacksAsync();
                _pendingFeedbackCount = _pendingFeedbacks.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento feedback pendenti: {ex.Message}");
                _pendingFeedbacks = new List<TicketPendingFeedback>();
                _pendingFeedbackCount = 0;
            }
        }

        private async Task OpenFeedbackDialog(TicketPendingFeedback ticket)
        {
            var result = await DialogService.OpenAsync<FeedbackDialog>(
                Localize["Rate Service"],
                new Dictionary<string, object>
                {
                    { "Ticket", ticket }
                },
                new DialogOptions
                {
                    Width = "450px",
                    Height = "auto",
                    Resizable = false,
                    Draggable = true
                });

            if (result is bool success && success)
            {
                // Ricarica i feedback pendenti dopo il submit
                await LoadPendingFeedbacks();
                StateHasChanged();
            }
        }

        private async Task SkipFeedback(int ticketId)
        {
            var confirmed = await DialogService.Confirm(
                Localize["Are you sure you want to skip this feedback?"],
                Localize["Skip Feedback"],
                new ConfirmOptions
                {
                    OkButtonText = Localize["Skip"],
                    CancelButtonText = Localize["Cancel"]
                });

            if (confirmed == true)
            {
                await FeedbackService.SkipFeedbackAsync(ticketId);
                await LoadPendingFeedbacks();
                StateHasChanged();
            }
        }

        protected void AddTicket()
        {
            NavigationManager.NavigateTo("/Tickets/Create");
        }

        private void ComapanyDetails()
        {
            NavigationManager.NavigateTo("/Companies/Customer/Details");
        }

        private void TicketWorking()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.Working}");
        }

        private void Tickets()
        {
            NavigationManager.NavigateTo($"/Tickets/Index");
        }

        private void Articles()
        {
            NavigationManager.NavigateTo($"/Articles");
        }

        private void TicketsNewMessage()
        {
            NavigationManager.NavigateTo($"/DashBoard/Tickets/{(int)TicketTypeSearch.NewMessage}");
        }

        public void Dispose()
        {
            DynamicNotificationHandlers.Unregister(this);
        }
    }
}
