using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Deals
{
    public partial class Forecast : ComponentBase
    {
        [Inject] private IDealService DealService { get; set; } = default!;
        [Inject] private IBaseRestService<ApplicationUser, UsersFilterModel, string> UserService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;
        private CommercialForecastDTO? _forecast;
        private List<ApplicationUser> _users = new();
        private DateTime? _dateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime? _dateTo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(6).AddDays(-1);
        private string? _idUser;
        private bool _loading;

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await LoadUsers();
            await LoadForecast();
        }

        private async Task LoadUsers()
        {
            var response = await UserService.GetList(new UsersFilterModel());
            _users = response?.Items?.ToList() ?? new List<ApplicationUser>();
        }

        private async Task LoadForecast()
        {
            _loading = true;
            try
            {
                _forecast = await DealService.GetForecastAsync(new DealForecastFilter
                {
                    DateFrom = _dateFrom,
                    DateTo = _dateTo,
                    IdUser = _idUser
                });
            }
            finally
            {
                _loading = false;
            }
        }
    }
}
