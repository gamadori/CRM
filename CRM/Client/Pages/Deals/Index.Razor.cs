using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Deals
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        private IDealService DealService { get; set; } = default!;

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> UserService { get; set; } = default!;

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        private IEnumService EnumService { get; set; } = default!;

        [Inject]
        private DialogService DialogService { get; set; } = default!;

        [Inject]
        private IHeaderService HeaderService { get; set; } = default!;

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public string? IdUser { get; set; }

        [Parameter]
        public int? IdArticle { get; set; }

        [Parameter]
        public int? IdProject { get; set; }

        [Parameter]
        public int TypeSearch { get; set; } = (int)TicketTypeSearch.All;

        [Parameter]
        public string PageTitle { get; set; } = "Opportunita";

        [Parameter]
        public Action<int>? OnClickDetails { get; set; }

        [Parameter]
        public Action<int?>? OnClickEdit { get; set; }

        [Parameter]
        public Action<int>? OnClickDelete { get; set; }

        [Parameter]
        public Action<int>? OnGotoIndex { get; set; }

        private PagingResponse<DealDTO, decimal> _deals = new()
        {
            Items = new List<DealDTO>(),
            MetaData = new PagingHeaderModel()
        };

        private readonly int _pageSize = 10;
        private int _pageNumber = 1;
        private bool _isLoading;
        private string? _search;
        private string? _idUser;
        private DealStates? _state;
        private DealPhases? _phase;
        private List<ApplicationUser> _users = new();
        private List<EnumField> _states = new();
        private List<EnumField> _phases = new();
        private RadzenDataGrid<DealDTO>? grdDeals;

        private PageHeaderModel? _pageHeader;

        private int TotalCount => _deals.MetaData?.TotalCount ?? _deals.Items.Count;

        protected override async Task OnInitializedAsync()
        {
            _idUser = IdUser;
            _pageHeader = await HeaderService.Create();
            _states = EnumService.EnumGetList(typeof(DealStates));
            _phases = EnumService.EnumGetList(typeof(DealPhases));
            await LoadUsers();
            await LoadDeals(0, _pageSize, "Date desc");
        }

        private async Task LoadData(LoadDataArgs args)
        {
            _pageNumber = ((args.Skip ?? 0) / _pageSize) + 1;
            await LoadDeals(args.Skip ?? 0, args.Top ?? _pageSize, args.OrderBy);
        }

        private async Task LoadDeals(int skip = 0, int top = 10, string? orderBy = null)
        {
            _isLoading = true;
            try
            {
                var filter = new DealFilter
                {
                    Skip = skip,
                    Top = top,
                    PageSize = _pageSize,
                    IdUser = _idUser,
                    Search = _search,
                    State = _state,
                    Phase = _phase,
                    OrderBy = string.IsNullOrWhiteSpace(orderBy) ? "Date desc" : orderBy
                };

                _deals = await DealService.GetSummaryAsync(filter) ?? new PagingResponse<DealDTO, decimal>
                {
                    Items = new List<DealDTO>(),
                    MetaData = new PagingHeaderModel()
                };
            }
            finally
            {
                _deals.Items ??= new List<DealDTO>();
                _deals.MetaData ??= new PagingHeaderModel();
                _isLoading = false;
            }
        }

        private async Task LoadUsers()
        {
            var response = await UserService.GetList(new UsersFilterModel());
            _users = response?.Items?.ToList() ?? new List<ApplicationUser>();
        }

        private async Task OnSearchChanged(ChangeEventArgs args)
        {
            _search = args?.Value?.ToString();
            await ReloadGrid();
        }

        private async Task ClearFilters()
        {
            _search = null;
            _idUser = IdUser;
            _state = null;
            _phase = null;
            await ReloadGrid();
        }

        private async Task ReloadGrid()
        {
            if (grdDeals != null)
                await grdDeals.FirstPage(true);
        }

        private void NewDeal()
        {
            if (OnClickEdit != null)
            {
                OnClickEdit(null);
            }
            else
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/New");
            }
        }

        private void Details(int idDeal)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idDeal);
            }
            else
            {
                // Apre il contenitore a tab (Dati/Allegati/Preventivi/Tickets), non la scheda singola
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{idDeal}");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
            {
                OnClickEdit(id);
            }
            else
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{id}/Edit");
            }
        }

        private async Task Delete(int id)
        {
            if (await DialogService.Confirm(Localize["Eliminare il Deal selezionato"], Localize["Elimina"]) == true)
            {
                if (OnClickDelete != null)
                {
                    OnClickDelete(id);
                }
                else
                {
                    await DealService.DeleteAsync(id);
                    await ReloadGrid();
                }
            }
        }

        private string StateText(DealStates state)
        {
            return EnumService.Get(typeof(DealStates), state);
        }

        private string PhaseText(DealPhases phase)
        {
            return EnumService.Get(typeof(DealPhases), phase);
        }

        private static string StateClass(DealStates state)
        {
            return state switch
            {
                DealStates.Open => "is-open",
                DealStates.Suspended => "is-paused",
                DealStates.CloseWon => "is-won",
                DealStates.CloseLost => "is-lost",
                _ => "is-muted"
            };
        }

        private static string StateBadgeClass(DealStates state)
        {
            return state switch
            {
                DealStates.Open => "bg-success",
                DealStates.Suspended => "bg-warning text-dark",
                DealStates.CloseWon => "bg-primary",
                DealStates.CloseLost => "bg-danger",
                _ => "bg-secondary"
            };
        }

        private static int PhaseProgress(DealPhases phase)
        {
            return phase switch
            {
                DealPhases.InitialContact => 15,
                DealPhases.NeedsChecked => 35,
                DealPhases.DecisionMakingPhase => 55,
                DealPhases.OfferSubmitted => 75,
                DealPhases.Obtained => 100,
                DealPhases.Lost => 100,
                _ => 0
            };
        }
    }
}
