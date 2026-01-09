using BlazoringComponents;
using CRM.Client.Helpers;
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

namespace CRM.Client.Pages.TicketStates
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        ITicketStatesService TicketService { get; set; } = default!;

        [Inject]
        DialogService DialogService { get; set; } = default!;

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        private List<TicketState>? _items;

        private TicketStateFilter _filter = new TicketStateFilter();

        private int _totalCount = 0;

        private RadzenDataGrid<TicketState> _grdTicketState = default!;

        private bool _loading = true;

        private List<TicketState> _itemsToUpdate = new List<TicketState>();

        private List<TicketState> _itemsToInsert = new List<TicketState>();

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

            var resp = await TicketService.GetPagingAsync(_filter);

            _items = resp?.Items ?? new List<TicketState>();
            _totalCount = resp?.MetaData?.TotalCount ?? 0;

            // Initialize nullable enum helper idState from underlying int State
            if (_items != null)
            {
                foreach (var it in _items)
                {
                    try
                    {
                        it.idState = Enum.IsDefined(typeof(eTicketStates), it.State) ? (eTicketStates?)((eTicketStates)it.State) : null;
                    }
                    catch
                    {
                        it.idState = null;
                    }
                }
            }

            _loading = false;

            StateHasChanged();
        }

        private async Task Delete(TicketState item)
        {
            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente lo stato"]}: {item.Description}") == true)
            {
                await TicketService.DeleteAsync(item.Id);

                await LoadData();
            }
        }

        async Task EditRow(TicketState item)
        {
            if (!_grdTicketState.IsValid)
                return;

            Reset();

            _itemsToUpdate.Add(item);
            await _grdTicketState.EditRow(item);
        }

        private async Task OnUpdateRow(TicketState item)
        {
            Reset(item);

            // Sync nullable enum back to int field before saving
            if (item.idState.HasValue)
            {
                item.State = (int)item.idState.Value;
            }

            var resp = await TicketService.PostAsync(item);

            await _grdTicketState.Reload();

        }

        private void CancelEdit(TicketState item)
        {
            Reset(item);

            _grdTicketState.CancelEditRow(item);

        }

        private async Task DeleteRow(TicketState item)
        {
            Reset(item);

            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente lo stato"]}: {item.Description}?") != true)
            {
                _grdTicketState.CancelEditRow(item);
                return;
            }
            if (_items != null && _items.Contains(item))
            {
                await TicketService.DeleteAsync(item.Id);

                await _grdTicketState.Reload();
            }
            else
            {
                _grdTicketState.CancelEditRow(item);
                await _grdTicketState.Reload();
            }
        }

        async Task InsertRow()
        {
            if (!_grdTicketState.IsValid)
                return;

            Reset();

            var item = new TicketState();

            // initialize helper enum
            item.idState = null;

            _itemsToInsert.Add(item);
            await _grdTicketState.InsertRow(item);
        }

        async Task InsertAfterRow(TicketState row)
        {
            if (!_grdTicketState.IsValid) return;
            Reset();

            var item = new TicketState();
            item.idState = null;
            _itemsToInsert.Add(item);

            await _grdTicketState.InsertAfterRow(item, row);
        }

        async Task OnCreateRow(TicketState item)
        {
            // Sync enum to int before creating
            if (item.idState.HasValue)
            {
                item.State = (int)item.idState.Value;
            }

            var resp = await TicketService.PostAsync(item);
            Reset(item);
            await _grdTicketState.Reload();
        }


        void Reset(TicketState item)
        {
            _itemsToInsert.Remove(item);
            _itemsToUpdate.Remove(item);
        }


        private void Reset()
        {
            _itemsToUpdate.Clear();
            _itemsToInsert.Clear();
        }

        private async Task SaveRow(TicketState item)
        {
            if (!_grdTicketState.IsValid)
                return;
            await _grdTicketState.UpdateRow(item);
        }
        private void OnChange(string value, string name)
        {
        }
        private void OnError(UploadErrorEventArgs args, string name)
        {
        }
    }
}
