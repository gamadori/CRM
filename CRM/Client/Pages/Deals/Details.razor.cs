using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Deals
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        private IDealService DealService { get; set; } = default!;

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        private IEnumService EnumService { get; set; } = default!;

        [Inject]
        private IHeaderService HeaderService { get; set; } = default!;

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action? OnClickEdit { get; set; }

        [Parameter]
        public Action<int>? OnClickEditChild { get; set; }

        [Parameter]
        public Action? OnClickCancel { get; set; }

        private DealDTO? _deal;
        private bool _loading = true;

        private PageHeaderModel? _pageHeader;

        private int DealProgress => _deal == null ? 0 : PhaseProgress(_deal.Phase);

        protected override async Task OnInitializedAsync()
        {
            if (OnClickCancel == null)
                _pageHeader = await HeaderService.Create();

            await LoadDeal();
        }

        private async Task LoadDeal()
        {
            _loading = true;
            try
            {
                _deal = await DealService.GetItemAsync(Id);
            }
            finally
            {
                _loading = false;
            }
        }

        private void Edit()
        {
            if (OnClickEdit != null)
            {
                OnClickEdit();
            }
            else
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{Id}/Edit");
            }
        }

        private void Back()
        {
            if (OnClickCancel != null)
            {
                OnClickCancel();
            }
            else
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}");
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
    }
}
