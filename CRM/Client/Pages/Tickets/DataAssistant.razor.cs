using CRM.Client.Services;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    public partial class DataAssistant : ComponentBase
    {
        [Inject]
        ITicketsService Service { get; set; }

        [Inject]
        IJSRuntime JS { get; set; }

        private const string ScrollAreaId = "data-assistant-messages";

        private readonly List<ChatTurn> _turns = new();

        private string _input = string.Empty;

        private bool _loading = false;

        private async Task OnKeyDown(KeyboardEventArgs args)
        {
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
            StateHasChanged();
            await ScrollToBottom();

            try
            {
                var request = new AssistantChatRequest
                {
                    Messages = _turns
                        .Select(t => new AssistantChatMessage { Role = t.Role, Content = t.Content })
                        .ToList()
                };

                var response = await Service.DataAssistantAsk(request);

                _turns.Add(new ChatTurn
                {
                    Role = "assistant",
                    Content = response.Success
                        ? response.Reply
                        : $"⚠️ {response.Message ?? "Si è verificato un errore. Riprova."}"
                });
            }
            catch (Exception ex)
            {
                _turns.Add(new ChatTurn { Role = "assistant", Content = $"⚠️ Errore: {ex.Message}" });
            }
            finally
            {
                _loading = false;
                StateHasChanged();
                await ScrollToBottom();
            }
        }

        private async Task ScrollToBottom()
        {
            try
            {
                await JS.InvokeVoidAsync("scrollToBottom", ScrollAreaId);
            }
            catch
            {
                // JS helper non disponibile: ignora
            }
        }

        private class ChatTurn
        {
            public string Role { get; set; } = "user";
            public string Content { get; set; } = string.Empty;
        }
    }
}
