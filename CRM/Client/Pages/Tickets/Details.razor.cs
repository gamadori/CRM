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

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickTicketClose { get; set; }

        [Parameter]
        public EventCallback OnClickPrint { get; set; }

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

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            _pageHeader = await HeaderService.Create(PageMode);
            StateHasChanged();
        }

        private async Task LoadData()
        {
            try
            {
                if (Id != null)
                {
                    _ticket = await _service.GetDetails(Id.Value);
                    await LoadAssignedUsers();
                    if (_ticket.Closed)
                    {
                        await LoadFeedback();
                    }
                }
                else
                    _ticket = new TicketDTO();
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

        private async void TicketPrint()
        {
            if (OnClickPrint.HasDelegate)
                await OnClickPrint.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/Tickets/Report/{Id}");
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
