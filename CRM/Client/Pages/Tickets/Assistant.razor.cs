using CRM.Client.Services;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    public partial class Assistant : ComponentBase
    {
        [Inject]
        ITicketsService Service { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IJSRuntime JS { get; set; }

        private const string ScrollAreaId = "assistant-messages";

        private readonly List<ChatTurn> _turns = new();

        private string _input = string.Empty;

        private bool _loading = false;

        private async Task OnKeyDown(KeyboardEventArgs args)
        {
            // Invio = manda; Shift+Invio = a capo
            if (args.Key == "Enter" && !args.ShiftKey)
            {
                await SendAsync();
            }
        }

        private async Task SendAsync()
        {
            var text = _input?.Trim();
            if (string.IsNullOrWhiteSpace(text) || _loading)
                return;

            _turns.Add(new ChatTurn { Role = "user", Content = text });
            _input = string.Empty;
            _loading = true;

            // Turno assistente vuoto in cui far confluire lo streaming
            var assistantTurn = new ChatTurn { Role = "assistant", Content = string.Empty };

            // Storico da inviare: tutti i turni tranne il placeholder dell'assistente
            var request = new AssistantChatRequest
            {
                Messages = _turns
                    .Select(t => new AssistantChatMessage { Role = t.Role, Content = t.Content })
                    .ToList()
            };

            _turns.Add(assistantTurn);
            StateHasChanged();
            await ScrollToBottom();

            try
            {
                await Service.AssistantChatStream(
                    request,
                    onTickets: tickets =>
                    {
                        assistantTurn.Tickets = tickets;
                        InvokeAsync(StateHasChanged);
                    },
                    onChunk: chunk =>
                    {
                        assistantTurn.Content += chunk;
                        InvokeAsync(StateHasChanged);
                    },
                    onError: error =>
                    {
                        if (string.IsNullOrEmpty(assistantTurn.Content))
                            assistantTurn.Content = $"⚠️ {error}";
                        InvokeAsync(StateHasChanged);
                    });
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(assistantTurn.Content))
                    assistantTurn.Content = $"⚠️ Errore: {ex.Message}";
            }
            finally
            {
                _loading = false;
                StateHasChanged();
                await ScrollToBottom();
            }
        }

        private async Task OpenTicket(int id)
        {
            await DialogService.OpenAsync<Summary>("Ticket",
                new Dictionary<string, object>() { { "Id", id } },
                new DialogOptions() { Height = "auto", Width = "100%", Top = "0px" });
        }

        private async Task ScrollToBottom()
        {
            try
            {
                await JS.InvokeVoidAsync("scrollToBottom", ScrollAreaId);
            }
            catch
            {
                // JS helper non disponibile: ignora (lo scroll non è critico)
            }
        }

        private class ChatTurn
        {
            public string Role { get; set; } = "user";
            public string Content { get; set; } = string.Empty;
            public List<TicketSimilarityResult> Tickets { get; set; }
        }
    }
}
