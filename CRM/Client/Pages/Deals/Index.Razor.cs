using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
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

        private int TotalCount => _deals.MetaData?.TotalCount ?? _deals.Items.Count;

        private int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)_pageSize));

        private bool CanGoPrevious => _pageNumber > 1;

        private bool CanGoNext => _pageNumber < TotalPages;

        private decimal PageAmount => _deals.Items.Sum(x => x.Amount);

        private decimal WonAmount => _deals.Items.Where(x => x.State == DealStates.CloseWon).Sum(x => x.Amount);

        private decimal OpenAmount => _deals.Items.Where(x => x.State == DealStates.Open).Sum(x => x.Amount);

        private decimal TargetAmount => _deals.Items.Sum(x => x.Target);

        protected override async Task OnInitializedAsync()
        {
            _idUser = IdUser;
            _states = EnumService.EnumGetList(typeof(DealStates));
            _phases = EnumService.EnumGetList(typeof(DealPhases));
            await LoadUsers();
            await LoadDeals();
        }

        private async Task LoadDeals(bool resetPage = false)
        {
            if (resetPage)
            {
                _pageNumber = 1;
            }

            _isLoading = true;
            try
            {
                var filter = new DealFilter
                {
                    Skip = (_pageNumber - 1) * _pageSize,
                    Top = _pageSize,
                    PageSize = _pageSize,
                    IdUser = _idUser,
                    Search = _search,
                    State = _state,
                    Phase = _phase,
                    OrderBy = "Date desc"
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

        private async Task ApplyFilters()
        {
            await LoadDeals(true);
        }

        private async Task ClearFilters()
        {
            _search = null;
            _idUser = IdUser;
            _state = null;
            _phase = null;
            await LoadDeals(true);
        }

        private async Task PreviousPage()
        {
            if (!CanGoPrevious)
            {
                return;
            }

            _pageNumber--;
            await LoadDeals();
        }

        private async Task NextPage()
        {
            if (!CanGoNext)
            {
                return;
            }

            _pageNumber++;
            await LoadDeals();
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{idDeal}/Details");
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
                    await LoadDeals();
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
