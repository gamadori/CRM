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
    /// <summary>
    /// Assistente AI unificato: un'unica chat per interrogare i dati del CRM
    /// (clienti, macchine, ticket…) e per cercare soluzioni a problemi tecnici
    /// nei ticket chiusi e nella knowledge base. Le risposte arrivano in streaming
    /// e sono renderizzate in Markdown con link alle schede del CRM.
    /// </summary>
    public partial class Assistant : ComponentBase, IAsyncDisposable
    {
        [Inject]
        IAssistantService Service { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IJSRuntime JS { get; set; }

        /// <summary>Ticket di contesto (dalla route /Tickets/Assistant/{IdTicket}): il suo modello guida la conoscenza.</summary>
        [Parameter]
        public int? IdTicket { get; set; }

        /// <summary>Modello/prodotto di contesto (query string ?IdProduct=): forza la conoscenza di quel prodotto.</summary>
        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdProduct { get; set; }

        private const string ScrollAreaId = "assistant-messages";

        private const string InputId = "assistant-input";

        private ElementReference _inputRef;

        private static readonly string[] Suggestions =
        {
            "Che macchine ha il cliente Rossi?",
            "Quanti ticket aperti abbiamo?",
            "Il gestionale non stampa le fatture dopo l'aggiornamento",
        };

        private readonly List<ChatTurn> _turns = new();

        private string _input = string.Empty;

        private bool _loading = false;

        /// <summary>Modulo JS collocato al componente (Assistant.razor.js).</summary>
        private IJSObjectReference _js;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    // Sopprime il newline di default su Invio (senza Shift): l'invio del
                    // messaggio è gestito da OnKeyDown e il newline, arrivando dopo la
                    // pulizia del campo, lo ri-riempirebbe.
                    _js = await JS.InvokeAsync<IJSObjectReference>("import", "./Pages/Tickets/Assistant.razor.js");
                    await _js.InvokeVoidAsync("preventEnterNewline", InputId);
                }
                catch
                {
                    // Modulo JS non caricabile: al peggio l'Invio inserisce un a-capo.
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_js != null)
            {
                try { await _js.DisposeAsync(); } catch { /* circuito già chiuso */ }
            }
        }

        private async Task OnKeyDown(KeyboardEventArgs args)
        {
            // Invio = manda; Shift+Invio = a capo
            if (args.Key == "Enter" && !args.ShiftKey)
            {
                await SendAsync();
            }
        }

        private async Task SendSuggestion(string text)
        {
            _input = text;
            await SendAsync();
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
                    .ToList(),
                // Contesto opzionale: se la chat parte da un ticket/prodotto, dà priorità alla sua conoscenza
                IdTicket = IdTicket,
                IdProduct = IdProduct
            };

            _turns.Add(assistantTurn);
            StateHasChanged();
            await ScrollToBottom();

            try
            {
                await Service.ChatStream(request, streamEvent =>
                {
                    switch (streamEvent.Type)
                    {
                        case AssistantStreamEvent.TypeStatus:
                            assistantTurn.Status = streamEvent.Text;
                            break;

                        case AssistantStreamEvent.TypeDelta:
                            assistantTurn.Status = null;
                            assistantTurn.Content += streamEvent.Text;
                            break;

                        case AssistantStreamEvent.TypeTickets:
                            assistantTurn.Tickets = streamEvent.Tickets;
                            break;

                        case AssistantStreamEvent.TypeLogId:
                            assistantTurn.LogId = streamEvent.LogId;
                            break;

                        case AssistantStreamEvent.TypeError:
                            assistantTurn.Status = null;
                            if (string.IsNullOrEmpty(assistantTurn.Content))
                                assistantTurn.Content = $"⚠️ {streamEvent.Text}";
                            break;
                    }

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
                assistantTurn.Status = null;
                _loading = false;
                StateHasChanged();
                await ScrollToBottom();
                await FocusInputAsync();
            }
        }

        /// <summary>
        /// Riporta il focus sul campo domanda: la textarea viene disabilitata durante
        /// l'elaborazione e la disabilitazione fa perdere il focus, che va restituito
        /// per poter proseguire la conversazione senza cliccare di nuovo nel campo.
        /// </summary>
        private async Task FocusInputAsync()
        {
            try
            {
                await _inputRef.FocusAsync();
            }
            catch
            {
                // Elemento non ancora renderizzato o circuito chiuso: il focus non è critico
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

        private async Task VoteUp(ChatTurn turn)
        {
            await SubmitFeedback(turn, 1, null);
        }

        private void VoteDown(ChatTurn turn)
        {
            // Il voto negativo apre il campo commento: il "perché" è il dato più utile.
            turn.ShowComment = true;
            StateHasChanged();
        }

        private async Task SubmitDownWithComment(ChatTurn turn)
        {
            var comment = string.IsNullOrWhiteSpace(turn.CommentDraft) ? null : turn.CommentDraft.Trim();
            await SubmitFeedback(turn, -1, comment);
        }

        private async Task SubmitFeedback(ChatTurn turn, int vote, string comment)
        {
            if (turn.LogId == null)
                return;

            var ok = await Service.SendFeedback(new AssistantFeedbackRequest
            {
                LogId = turn.LogId.Value,
                Vote = vote,
                Comment = comment
            });

            if (ok)
            {
                turn.Feedback = vote;
                turn.FeedbackSent = true;
                turn.ShowComment = false;
            }
            StateHasChanged();
        }

        private class ChatTurn
        {
            public string Role { get; set; } = "user";

            public string Content { get; set; } = string.Empty;

            /// <summary>Attività in corso lato server ("Cerco il cliente…"): mostrata durante l'uso dei tool.</summary>
            public string Status { get; set; }

            public List<TicketSimilarityResult> Tickets { get; set; }

            /// <summary>Id del log lato server: presente quando la risposta è completa e votabile.</summary>
            public int? LogId { get; set; }

            /// <summary>Voto dato dall'operatore: 1 = su, -1 = giù, null = nessuno.</summary>
            public int? Feedback { get; set; }

            public bool FeedbackSent { get; set; }

            public bool ShowComment { get; set; }

            public string CommentDraft { get; set; } = string.Empty;
        }
    }
}
