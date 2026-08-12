using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private ITicketsService _service { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        ITicketFeedbackService FeedbackService { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        ICurrentUserService CurrentUserService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickTicketClose { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        [Parameter]
        public bool HeaderVisible { get; set; } = false;

        [Parameter]
        public string BackUrl { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private bool _isDownloadingPdf = false;
        private TicketDTO _ticket = null;
        private List<ApplicationUser> _assignedUsers = new List<ApplicationUser>();
        private bool _isLoadingUsers = false;
        private PageHeaderModel? _pageHeader = null;
        private TicketFeedbackResponse _feedback = null;
        private bool _isLoadingFeedback = false;
        private string _summaryDraft = string.Empty;
        private string? _summaryMessage = null;
        private bool _summaryMessageIsError = false;
        private bool _isProposingSummary = false;
        private bool _isSavingSummary = false;
        private bool _isEditingSummary = false;
        private bool _isClaiming = false;
        private bool _isChangingBlock = false;
        private bool _isHandlingAiRouting = false;

        /// <summary>Email in arrivo da cui è nato (o a cui è agganciato) il ticket, se presente.</summary>
        private int? _idInboundEmail;

        /// <summary>
        /// Lo smistamento AI si mostra a chi puo' assegnare il ticket ed e' dell'azienda madre:
        /// sono due assi diversi e servono entrambi, perche' un utente di un'altra azienda puo'
        /// avere ruolo Standard. Il server non manda nemmeno il dato, questo e' il secondo cancello.
        /// </summary>
        private bool _canViewAiRouting;

        protected override async Task OnInitializedAsync()
        {
            var user = await CurrentUserService.Get();
            _canViewAiRouting = user?.CanTicketAssign == true && user?.CanViewInternalData == true;

            await LoadData();
            await LoadInboundEmail();
            _pageHeader = await HeaderService.Create(PageMode);
            StateHasChanged();
        }

        private async Task LoadInboundEmail()
        {
            if (Id == null) return;
            try
            {
                var email = await HttpClient.GetFromJsonAsync<CRM.Shared.InboundEmail>($"api/InboundEmails/by-ticket/{Id}");
                _idInboundEmail = email?.Id;
            }
            catch
            {
                _idInboundEmail = null;
            }
        }

        private async Task LoadData()
        {
            try
            {
                if (Id != null)
                {
                    _ticket = await _service.GetDetails(Id.Value);
                    _summaryDraft = _ticket?.OperationalSummary ?? string.Empty;
                    await LoadAssignedUsers();
                    if (_ticket.Closed)
                    {
                        await LoadFeedback();
                    }
                }
                else
                {
                    _ticket = new TicketDTO();
                    _summaryDraft = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task LoadFeedback()
        {
            if (Id == null) return;

            try
            {
                _isLoadingFeedback = true;

                _feedback = await FeedbackService.GetFeedbackByTicketAsync(Id.Value);

                
                if (_feedback != null)
                {
                    try
                    {
                        await FeedbackService.MarkAsReadAsync(_feedback.Id);

                       
                    }
                    catch { }
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                _feedback = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento feedback: {ex.Message}");
                _feedback = null;
            }
            finally
            {
                _isLoadingFeedback = false;
            }
        }

        private async Task LoadAssignedUsers()
        {
            if (Id == null) return;

            try
            {
                _isLoadingUsers = true;
                _assignedUsers.Clear();
                await InvokeAsync(StateHasChanged);
                
                var userIds = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{Id}/assigned-users");
                
                if (userIds != null && userIds.Any())
                {
                    foreach (var userId in userIds)
                    {
                        try
                        {
                            var user = await HttpClient.GetFromJsonAsync<ApplicationUser>($"api/Users/{userId}");
                            if (user != null)
                            {
                                _assignedUsers.Add(user);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore caricamento utente {userId}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati: {ex.Message}");
                _assignedUsers.Clear();
            }
            finally
            {
                _isLoadingUsers = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ProposeSummary()
        {
            if (Id == null)
                return;

            try
            {
                _isProposingSummary = true;
                _summaryMessage = null;
                _summaryMessageIsError = false;

                var proposal = await _service.ProposeSummary(Id.Value, new TicketSummaryProposalRequest());
                _summaryDraft = proposal.Summary ?? string.Empty;
                _isEditingSummary = true;
                _summaryMessage = proposal.Warning
                    ?? $"Bozza generata da {(proposal.GeneratedByAi ? "AI" : "chat")} su {proposal.SourceMessageCount} messaggi.";
            }
            catch (Exception ex)
            {
                _summaryMessage = $"Errore durante la proposta del riepilogo: {ex.Message}";
                _summaryMessageIsError = true;
                NotificationService?.Notify(NotificationSeverity.Error, "Riepilogo operativo", _summaryMessage);
            }
            finally
            {
                _isProposingSummary = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task SaveSummary()
        {
            if (Id == null)
                return;

            try
            {
                _isSavingSummary = true;
                _summaryMessage = null;
                _summaryMessageIsError = false;

                var updatedTicket = await _service.UpdateSummary(Id.Value, new UpdateTicketSummaryRequest
                {
                    Summary = _summaryDraft ?? string.Empty
                });

                if (updatedTicket == null)
                {
                    _summaryMessage = "Riepilogo non salvato.";
                    _summaryMessageIsError = true;
                    NotificationService?.Notify(NotificationSeverity.Error, "Riepilogo operativo", _summaryMessage);
                    return;
                }

                _ticket = updatedTicket;
                _summaryDraft = _ticket.OperationalSummary ?? string.Empty;
                _isEditingSummary = false;
                _summaryMessage = "Riepilogo salvato.";
                NotificationService?.Notify(NotificationSeverity.Success, "Riepilogo operativo", _summaryMessage);
            }
            catch (Exception ex)
            {
                _summaryMessage = $"Errore durante il salvataggio del riepilogo: {ex.Message}";
                _summaryMessageIsError = true;
                NotificationService?.Notify(NotificationSeverity.Error, "Riepilogo operativo", _summaryMessage);
            }
            finally
            {
                _isSavingSummary = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private void EditSummary()
        {
            _summaryDraft = _ticket?.OperationalSummary ?? string.Empty;
            _summaryMessage = null;
            _summaryMessageIsError = false;
            _isEditingSummary = true;
        }

        private void CancelSummaryEdit()
        {
            _summaryDraft = _ticket?.OperationalSummary ?? string.Empty;
            _summaryMessage = null;
            _summaryMessageIsError = false;
            _isEditingSummary = false;
        }

        /// <summary>
        /// Mostra i dettagli del feedback in un dialog
        /// </summary>
        private async Task ShowFeedbackDetails()
        {
            if (_feedback == null) return;

            await DialogService.OpenAsync<FeedbackDetailsDialog>(
                "Feedback Cliente",
                new Dictionary<string, object> { { "Feedback", _feedback } },
                new DialogOptions
                {
                    Width = "450px",
                    Height = "auto",
                    CloseDialogOnEsc = true,
                    CloseDialogOnOverlayClick = true,
                    ShowClose = true
                }
            );
        }

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Tickets/{Id}/Edit");
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                BackToUrl();
        }

        protected void TicketClose()
        {
            if (OnClickTicketClose != null)
                OnClickTicketClose();
            else
                NavigationManager.NavigateTo($"/Tickets/Close/{Id}");
        }

        private async Task BlockTicket()
        {
            if (Id == null || _isChangingBlock)
                return;

            var reason = await DialogService.OpenAsync<TicketBlockDialog>(
                "Segnala blocco",
                new Dictionary<string, object>(),
                new DialogOptions
                {
                    Width = "560px",
                    CloseDialogOnEsc = true,
                    CloseDialogOnOverlayClick = false,
                    ShowClose = true
                });

            if (reason == null)
                return;

            await ChangeBlockStateAsync(
                () => _service.BlockAsync(Id.Value, new TicketBlockRequest { Reason = reason.ToString() ?? string.Empty }),
                "Blocco ticket");
        }

        private async Task UnblockTicket()
        {
            if (Id == null || _isChangingBlock)
                return;

            var note = await DialogService.OpenAsync<TicketBlockDialog>(
                "Sblocca ticket",
                new Dictionary<string, object>
                {
                    { "Resolve", true }
                },
                new DialogOptions
                {
                    Width = "560px",
                    CloseDialogOnEsc = true,
                    CloseDialogOnOverlayClick = false,
                    ShowClose = true
                });

            if (note == null)
                return;

            await ChangeBlockStateAsync(
                () => _service.UnblockAsync(Id.Value, new TicketUnblockRequest { ResolutionNote = note.ToString() }),
                "Sblocco ticket");
        }

        private async Task ChangeBlockStateAsync(Func<Task<CRM.Client.Models.APIResponseMessage<TicketDTO>>> action, string title)
        {
            try
            {
                _isChangingBlock = true;
                var response = await action();
                if (!response.State)
                {
                    NotificationService?.Notify(NotificationSeverity.Error, title, response.Message ?? "Operazione non riuscita");
                    return;
                }

                if (response.Data != null)
                {
                    _ticket = response.Data;
                    _summaryDraft = _ticket.OperationalSummary ?? string.Empty;
                }

                NotificationService?.Notify(NotificationSeverity.Success, title, response.Message ?? "Operazione completata");
            }
            finally
            {
                _isChangingBlock = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task DownloadPdf()
        {
            try
            {
                if (Id == null || Id <= 0)
                    return;

                _isDownloadingPdf = true;
                StateHasChanged();

                var response = await HttpClient.GetAsync($"api/Tickets/pdf/{Id}");

                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = $"Ticket_{Id}_{DateTime.Now:yyyyMMdd}.pdf";

                    await JSRuntime.InvokeVoidAsync("downloadFileFromBytes", 
                        fileName, 
                        "application/pdf", 
                        fileBytes);
                }
                else
                {
                    Console.WriteLine($"Errore download PDF: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore download PDF: {ex.Message}");
            }
            finally
            {
                _isDownloadingPdf = false;
                StateHasChanged();
            }
        }

        private async Task PrepareAssign()
        {
            var result = await DialogService.OpenAsync<Assign>(
                Localize["Assign Users"],
                new Dictionary<string, object> { { "Id", Id }, { "Date", _ticket.Date }, {"TicketTypeId", _ticket.IdType } },
                new DialogOptions 
                { 
                    Width = "900px", 
                    Height = "650px",
                    Resizable = true,
                    Draggable = true,
                    CloseDialogOnEsc = true
                }
            );

            Console.WriteLine($"[Details] Dialog chiuso. Result: {result}");
            Console.WriteLine($"[Details] Ricaricamento dati ticket #{Id}...");
            await LoadData();
            Console.WriteLine($"[Details] Utenti assegnati dopo reload: {_assignedUsers.Count}");
            await InvokeAsync(StateHasChanged);
        }

        private async Task ClaimTicket()
        {
            if (Id == null || _isClaiming)
                return;

            try
            {
                _isClaiming = true;
                var response = await _service.ClaimAsync(Id.Value);
                if (!response.State)
                {
                    NotificationService?.Notify(NotificationSeverity.Error, "Presa in carico", response.Message ?? "Operazione non riuscita");
                    return;
                }

                if (response.Data != null)
                {
                    _ticket = response.Data;
                    _summaryDraft = _ticket.OperationalSummary ?? string.Empty;
                }

                await LoadAssignedUsers();
                NotificationService?.Notify(NotificationSeverity.Success, "Presa in carico", response.Message ?? "Ticket preso in carico");
            }
            finally
            {
                _isClaiming = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// Accetta o scarta il gruppo proposto dallo smistamento AI. Il ticket ricaricato dal
        /// server e' la sola fonte di verita': stato, gruppo ed esito cambiano insieme.
        /// </summary>
        private async Task HandleAiRouting(bool accept)
        {
            if (Id == null || _isHandlingAiRouting)
                return;

            try
            {
                _isHandlingAiRouting = true;

                var response = accept
                    ? await _service.AcceptAiRoutingAsync(Id.Value)
                    : await _service.DismissAiRoutingAsync(Id.Value);

                var title = accept ? "Assegnazione al gruppo" : "Suggerimento";

                if (!response.State)
                {
                    NotificationService?.Notify(NotificationSeverity.Error, title, response.Message ?? "Operazione non riuscita");
                    return;
                }

                if (response.Data != null)
                    _ticket = response.Data;

                NotificationService?.Notify(NotificationSeverity.Success, title, response.Message ?? "Operazione completata");
            }
            finally
            {
                _isHandlingAiRouting = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected void SendInvitation() { }

        private string GetUserInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "?";

            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        private string GetContrastTextColor(string backgroundColor)
        {
            if (string.IsNullOrWhiteSpace(backgroundColor))
                return "#000000";

            var hex = backgroundColor.TrimStart('#');

            if (hex.Length == 3)
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

            if (hex.Length != 6)
                return "#000000";

            try
            {
                var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
                return luminance > 0.5 ? "#000000" : "#ffffff";
            }
            catch
            {
                return "#000000";
            }
        }

        private string GetContrastBackgroundColor(string backgroundColor)
        {
            return backgroundColor ?? "#6c757d";
        }

        private void BackToUrl()
        {
            if (BackUrl == null || BackUrl.Length == 0)
                BackUrl = "/Tickets/Index";
            else
                BackUrl = BackUrl.Replace("-", "/");

            NavigationManager.NavigateTo($"/Tickets/Index/{BackUrl}");
        }
    }
}
