
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;


namespace CRM.Client.Pages.Logos
{
    
    public partial class Index : ComponentBase
    {

        [Inject]
        NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        ILogosService LogosService { get; set; } = default!;

        [Inject]
        DialogService DialogService { get; set; } = default!;

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        IHeaderService HeaderService { get; set; } = default!;

        private List<Logo>? _logos = null;

        private LogosFilterModel _filter = new LogosFilterModel();

        private int _totalCount = 0;

        private RadzenDataGrid<Logo> _grdLogos = default!;

        private bool _loading = true;

        private List<Logo> _logosToUpdate = new List<Logo>();

        private List<Logo> _logosToInsert = new List<Logo>();

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        private async Task LoadData(LoadDataArgs? args = null)
        {
            _loading = true;
            if (args != null)
            {
                _filter.Skip = args?.Skip;
                _filter.Top = args?.Top;

                _filter.OrderBy = args?.OrderBy;
                _filter.Filter = args?.Filter;

            }

            var resp = await LogosService.GetPagingAsync(_filter);

            _logos = resp?.Items ?? new List<Logo>();
            _totalCount = resp?.MetaData?.TotalCount ?? 0;
            _loading = false;

            _pageHeader = await HeaderService.Create();

            StateHasChanged();
        }

        private async Task Delete(Logo item)
        {
            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente il logo"]}: {item.Descrizione}") == true)
            {
                await LogosService.DeleteAsync(item.Id);

                await LoadData();
            }
        }

        async Task EditRow(Logo item)
        {
            if (!_grdLogos.IsValid)
                return;

            Reset();

            _logosToUpdate.Add(item);
            await _grdLogos.EditRow(item);
        }

        private async Task OnUpdateRow(Logo item)
        {
            Reset(item);

            var resp = await LogosService.PostAsync(item);

            await _grdLogos.Reload();

        }

        private void CancelEdit(Logo logo)
        {
            Reset(logo);

            _grdLogos.CancelEditRow(logo);

        }

        private async Task DeleteRow(Logo logo)
        {
            Reset(logo);

            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente il logo"]}: {logo.Descrizione}?") != true)
            {
                _grdLogos.CancelEditRow(logo);
                return;
            }
            if (_logos != null && _logos.Contains(logo))
            {
                await LogosService.DeleteAsync(logo.Id);

                await _grdLogos.Reload();
            }
            else
            {
                _grdLogos.CancelEditRow(logo);
                await _grdLogos.Reload();
            }
        }

        async Task InsertRow()
        {
            if (!_grdLogos.IsValid)
                return;

            Reset();

            var logo = new Logo();

            _logosToInsert.Add(logo);
            await _grdLogos.InsertRow(logo);
        }

        async Task InsertAfterRow(Logo row)
        {
            if (!_grdLogos.IsValid) return;
            Reset();

            var logo = new Logo();
            _logosToInsert.Add(logo);

            await _grdLogos.InsertAfterRow(logo, row);
        }

        async Task OnCreateRow(Logo logo)
        {
            var resp = await LogosService.PostAsync(logo);
            Reset(logo);
            await _grdLogos.Reload();
        }



        void Reset(Logo logo)
        {
            _logosToInsert.Remove(logo);
            _logosToUpdate.Remove(logo);
        }


        private void Reset()
        {
            _logosToUpdate.Clear();
            _logosToUpdate.Clear();
        }

        private async Task SaveRow(Logo logo)
        {
            if (!_grdLogos.IsValid)
                return;
            await _grdLogos.UpdateRow(logo);
        }
        private void OnChange(string value, string name)
        {
        }
        private void OnError(UploadErrorEventArgs args, string name)
        {
        }
    }
}
