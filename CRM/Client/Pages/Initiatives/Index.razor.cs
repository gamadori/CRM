using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Initiatives
{
    public partial class Index : ComponentBase
    {
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IInitiativeService InitiativeService { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        /// <summary>
        /// Tipo preso dalla rotta: e' cosi' che "Fiere e campagne" e "Trasferte" sono due voci di
        /// menu distinte pur essendo la stessa pagina. Null = tutte.
        /// </summary>
        [Parameter] public string? KindFilter { get; set; }

        private PageHeaderModel? _pageHeader;
        private PagingResponse<InitiativeDTO, decimal> _items = new() { Items = new List<InitiativeDTO>(), MetaData = new PagingHeaderModel() };
        private const int PageSize = 10;
        private int _pageNumber = 1;
        private bool _loading;
        private string? _search;
        private InitiativeState? _state;
        private InitiativeKind? _kind;

        private int TotalCount => _items.MetaData?.TotalCount ?? _items.Items.Count;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        private bool CanGoPrevious => _pageNumber > 1;
        private bool CanGoNext => _pageNumber < TotalPages;
        private decimal CostInPage => _items.Items.Sum(x => x.CostTotal);

        /// <summary>Titolo che segue la rotta: la pagina e' una sola, il contesto no.</summary>
        private string PageTitle => _kind switch
        {
            InitiativeKind.Fair => "Fiere e campagne",
            InitiativeKind.Trip => "Trasferte",
            _ => "Iniziative"
        };

        /// <summary>
        /// Sottotitolo della lista filtrata. Le due misure non sono intercambiabili, ed e' il
        /// motivo per cui i due tipi hanno report distinti: in fiera si contano i contatti nuovi,
        /// in trasferta il ROI mente e restano costi e cose successe.
        /// </summary>
        private string KindSubtitle => _kind switch
        {
            InitiativeKind.Fair => "Le fiere a cui partecipiamo, con i contatti raccolti e il loro costo.",
            InitiativeKind.Trip => "Le trasferte e i giri clienti, con i costi sostenuti.",
            _ => "Fiere e trasferte."
        };

        protected override async Task OnParametersSetAsync()
        {
            _kind = ParseKind(KindFilter);
            _pageHeader = await HeaderService.Create();

            // Sulla rotta filtrata (/Initiatives/List/Fair) l'ultimo segmento e' il tipo, e
            // l'intestazione dedotta dall'indirizzo finiva per intitolare la pagina "Fair".
            // Il tipo lo conosce questa pagina: e' lei a dire come si chiama quello che mostra.
            if (_pageHeader != null && _kind != null)
            {
                _pageHeader.Title = PageTitle;
                _pageHeader.Subtitle = KindSubtitle;
            }

            await Load(true);
        }

        private static InitiativeKind? ParseKind(string? value)
            => Enum.TryParse<InitiativeKind>(value, ignoreCase: true, out var parsed) ? parsed : null;

        private async Task Load(bool resetPage = false)
        {
            if (resetPage) _pageNumber = 1;

            _loading = true;
            try
            {
                _items = await InitiativeService.GetSummaryAsync(new InitiativeFilter
                {
                    Search = _search,
                    Kind = _kind,
                    State = _state,
                    Skip = (_pageNumber - 1) * PageSize,
                    Top = PageSize,
                    PageSize = PageSize
                }) ?? new PagingResponse<InitiativeDTO, decimal> { Items = new List<InitiativeDTO>(), MetaData = new PagingHeaderModel() };
            }
            finally
            {
                _items.Items ??= new List<InitiativeDTO>();
                _items.MetaData ??= new PagingHeaderModel();
                _loading = false;
            }
        }

        private void New()
        {
            var kind = _kind ?? InitiativeKind.Trip;
            NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/New?kind={kind}");
        }

        private void Open(int id) => NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/{id}/Info");

        private void Edit(int id) => NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/{id}/Edit");

        private async Task Delete(InitiativeDTO item)
        {
            // Si dice cosa comporta davvero: l'iniziativa sparisce, cio' che ha prodotto resta.
            // Senza dirlo, chi cancella immagina la cascata e non lo fa, oppure la immagina e la fa.
            var confirmed = await DialogService.Confirm(
                $"Eliminare '{item.Name}'? Attivita', note spese, lead e opportunita' collegati NON vengono eliminati: perdono solo il riferimento all'iniziativa.",
                "Elimina iniziativa");

            if (confirmed == true)
            {
                await InitiativeService.DeleteAsync(item.Id);
                await Load();
            }
        }

        private async Task ApplyFilters() => await Load(true);

        private async Task ClearFilters()
        {
            _search = null;
            _state = null;
            await Load(true);
        }

        private async Task PreviousPage()
        {
            if (!CanGoPrevious) return;
            _pageNumber--;
            await Load();
        }

        private async Task NextPage()
        {
            if (!CanGoNext) return;
            _pageNumber++;
            await Load();
        }

        internal static string KindText(InitiativeKind kind) => kind switch
        {
            InitiativeKind.Fair => "Fiera",
            InitiativeKind.Trip => "Trasferta",
            InitiativeKind.Webinar => "Webinar",
            InitiativeKind.Conference => "Convegno",
            InitiativeKind.Mailing => "Mailing",
            _ => "Altro"
        };

        internal static string StateText(InitiativeState state) => state switch
        {
            InitiativeState.Planned => "Pianificata",
            InitiativeState.InProgress => "In corso",
            InitiativeState.Closed => "Chiusa",
            _ => "Annullata"
        };

        internal static string StateClass(InitiativeState state) => state switch
        {
            InitiativeState.Planned => "state-new",
            InitiativeState.InProgress => "state-active",
            InitiativeState.Closed => "state-won",
            _ => "state-lost"
        };

        private static string Period(InitiativeDTO item)
            => item.DateFrom.Date == item.DateTo.Date
                ? item.DateFrom.ToString("dd/MM/yyyy")
                : $"{item.DateFrom:dd/MM/yyyy} - {item.DateTo:dd/MM/yyyy}";

        /// <summary>
        /// Consuntivo contro budget. Nessuna percentuale quando il budget non c'e': un
        /// "sforamento del 100%" su un budget mai fissato non vuol dire niente.
        /// </summary>
        private static string BudgetText(InitiativeDTO item)
            => item.BudgetPlanned == null || item.BudgetPlanned == 0
                ? "-"
                : item.BudgetPlanned.Value.ToString("C");
    }
}
