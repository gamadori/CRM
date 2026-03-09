using BlazoringComponents.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    public partial class Search: ComponentBase
    {
        [Inject]
        ITicketsService Service { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        private TicketFilter _ticketFilter = new TicketFilter();

        private PagingResponse<TicketDTO> _tickets = null;

        private SemanticSearchRequest _searchRequest = new SemanticSearchRequest
        {
            TopResults = 10,
            MinSimilarityThreshold = 60.0,
            OnlyClosedTickets = true
        };

        private SemanticSearchResponse _searchResponse = null;

        private bool _loading = false;

        private bool _showTraditionalSearch = false;

        protected override async Task OnInitializedAsync()
        {
            _ticketFilter.Search = string.Empty;
            await base.OnInitializedAsync();
        }

        void OnSpeechCaptured(string speechValue, bool isAI, string name)
        {
            if (string.IsNullOrWhiteSpace(speechValue))
                return;

            if (isAI)
            {
                _searchRequest.ProblemDescription += speechValue;
            }
            else
            {
                _ticketFilter.Search += speechValue;
            }
        }

        protected async Task SearchInputKeyPressed(KeyboardEventArgs args)
        {
            if (args.Key == "Enter")
            {
                // Determina se usare AI o ricerca tradizionale
                if (!string.IsNullOrWhiteSpace(_searchRequest.ProblemDescription))
                {
                    await OnClickAISearch();
                }
                else if (!string.IsNullOrWhiteSpace(_ticketFilter.Search))
                {
                    await LoadData();
                }
            }
        }

        private async Task OnClickAISearch()
        {
            if (string.IsNullOrWhiteSpace(_searchRequest.ProblemDescription))
            {
                return;
            }

            _loading = true;
            _searchResponse = null;
            StateHasChanged();

            try
            {
                _searchResponse = await Service.SemanticSearch(_searchRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore ricerca AI: {ex.Message}");
            }
            finally
            {
                _loading = false;
                StateHasChanged();
            }
        }

        private async Task OnClick()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            _loading = true;
            _searchResponse = null;
            StateHasChanged();

            if (_ticketFilter.Search != string.Empty)
            {
                _tickets = await Service.Search(_ticketFilter);
            }

            _loading = false;
            StateHasChanged();
        }

        private async Task OpenTicket(int id)
        {
            await DialogService.OpenAsync<Summary>(Localize["Ticket"], 
                new Dictionary<string, object>() { {"Id",  id} },
                new DialogOptions() { Height = "auto", Width = "100%", Top = "0px" });
        }

        private string GetSimilarityColor(double percentage, bool darker = false)
        {
            // Colore gradiente basato sulla percentuale
            if (percentage >= 90)
                return darker ? "#15803d" : "#22c55e"; // Verde
            else if (percentage >= 75)
                return darker ? "#1d4ed8" : "#3b82f6"; // Blu
            else if (percentage >= 60)
                return darker ? "#c2410c" : "#f97316"; // Arancione
            else
                return darker ? "#b91c1c" : "#ef4444"; // Rosso
        }
    }
}
