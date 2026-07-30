using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CRM.Client.Program;

namespace CRM.Client.Pages.TicketChats
{
    /// <summary>
    /// Posta in arrivo delle chat dei ticket: una riga per conversazione con l'anteprima
    /// dell'ultimo messaggio non letto. Sostituisce l'elenco filtrato dei ticket a cui
    /// portavano i riquadri "nuovi messaggi" delle due dashboard, che mostrava i ticket
    /// senza far vedere cosa fosse stato scritto.
    /// </summary>
    [Authorize]
    public partial class Unread : ComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {
        [Inject]
        private ITicketChatsService Service { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IHeaderService HeaderService { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        private List<UnreadChatModel> _conversations = new List<UnreadChatModel>();

        private bool _loading = true;

        private PageHeaderModel? _pageHeader = null;

        private int _totalUnread => _conversations.Sum(x => x.UnreadCount);

        private string SummaryText
        {
            get
            {
                if (_loading)
                    return Localize["Caricamento..."];

                if (_conversations.Count == 0)
                    return Localize["Nessuna conversazione in attesa"];

                var messages = $"{_totalUnread} {Localize[_totalUnread == 1 ? "messaggio" : "messaggi"]}";
                var tickets = $"{_conversations.Count} {Localize[_conversations.Count == 1 ? "ticket" : "ticket"]}";

                return $"{messages} · {tickets}";
            }
        }

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            _pageHeader = await HeaderService.Create();

            await Load();
        }

        /// <summary>Un messaggio in arrivo via SignalR: la lista si riallinea da sola.</summary>
        public async Task Handle(MsgNotify notification, CancellationToken cancellationToken)
        {
            await InvokeAsync(async () => await Load());
        }

        private async Task Load()
        {
            _loading = true;
            StateHasChanged();

            _conversations = await Service.GetUnread();

            _loading = false;
            StateHasChanged();
        }

        private async Task Refresh() => await Load();

        /// <summary>
        /// Apre il ticket direttamente sulla scheda della chat: la rotta con l'id del messaggio
        /// e' quella che <see cref="Tickets.Info"/> usa per selezionare la vista Chat.
        /// </summary>
        private void OpenChat(UnreadChatModel conversation)
        {
            NavigationManager.NavigateTo($"/Tickets/{conversation.IdTicket}/chat/{conversation.IdChat}");
        }

        private void OnItemKeyPress(KeyboardEventArgs args, UnreadChatModel conversation)
        {
            if (args.Key == "Enter" || args.Key == " ")
                OpenChat(conversation);
        }

        private void GotoTickets()
        {
            NavigationManager.NavigateTo("/Tickets");
        }

        private string TicketLabel(UnreadChatModel conversation)
            => string.IsNullOrWhiteSpace(conversation.TicketNumber)
                ? $"#{conversation.IdTicket}"
                : conversation.TicketNumber;

        /// <summary>Iniziali del mittente: evita una chiamata per riga solo per l'avatar.</summary>
        private string Initials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
                return parts[0].Substring(0, 1).ToUpperInvariant();

            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        /// <summary>Data leggibile a colpo d'occhio: ora se e' di oggi, altrimenti giorno.</summary>
        private string RelativeDate(DateTime date)
        {
            var today = DateTime.Today;

            if (date.Date == today)
                return date.ToString("HH:mm");

            if (date.Date == today.AddDays(-1))
                return Localize["Ieri"];

            if (date.Date > today.AddDays(-7))
                return date.ToString("dddd");

            return date.ToString("d");
        }

        public void Dispose()
        {
            DynamicNotificationHandlers.Unregister(this);
        }
    }
}
