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

namespace CRM.Client.Pages.Leads
{
    public partial class Index : ComponentBase
    {
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private ILeadService LeadService { get; set; } = default!;
        [Inject] private IEnumService EnumService { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;
        private PagingResponse<LeadDTO, decimal> _leads = new() { Items = new List<LeadDTO>(), MetaData = new PagingHeaderModel() };
        private const int PageSize = 10;
        private int _pageNumber = 1;
        private bool _loading;
        private string? _search;
        private LeadStatus? _status;
        private LeadSource? _source;

        private int TotalCount => _leads.MetaData?.TotalCount ?? _leads.Items.Count;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        private bool CanGoPrevious => _pageNumber > 1;
        private bool CanGoNext => _pageNumber < TotalPages;
        private int QualifiedCount => _leads.Items.Count(x => x.Status == LeadStatus.Qualified);
        private double AverageScore => _leads.Items.Count == 0 ? 0 : _leads.Items.Average(x => x.Score);

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await LoadLeads();
        }

        private async Task LoadLeads(bool resetPage = false)
        {
            if (resetPage) _pageNumber = 1;

            _loading = true;
            try
            {
                _leads = await LeadService.GetSummaryAsync(new LeadFilter
                {
                    Search = _search,
                    Status = _status,
                    Source = _source,
                    Skip = (_pageNumber - 1) * PageSize,
                    Top = PageSize,
                    PageSize = PageSize
                }) ?? new PagingResponse<LeadDTO, decimal> { Items = new List<LeadDTO>(), MetaData = new PagingHeaderModel() };
            }
            finally
            {
                _leads.Items ??= new List<LeadDTO>();
                _leads.MetaData ??= new PagingHeaderModel();
                _loading = false;
            }
        }

        private void NewLead() => NavigationManager.NavigateTo($"/{ConstHelper.ClientLeadsPath}/New");

        private void Edit(int id) => NavigationManager.NavigateTo($"/{ConstHelper.ClientLeadsPath}/{id}/Edit");

        private async Task ConvertLead(LeadDTO lead)
        {
            if (await DialogService.Confirm($"Convertire '{lead.Name}' in opportunita?", "Conversione lead") != true)
            {
                return;
            }

            var response = await LeadService.ConvertAsync(lead.Id, new ConvertLeadRequest());
            if (response.State && response.Data != null)
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{response.Data.Id}");
            }
            else
            {
                await DialogService.Alert(response.Message, "Conversione non riuscita");
            }
        }

        private async Task Delete(int id)
        {
            if (await DialogService.Confirm("Eliminare il lead selezionato?", "Elimina") == true)
            {
                await LeadService.DeleteAsync(id);
                await LoadLeads();
            }
        }

        private async Task ApplyFilters() => await LoadLeads(true);

        private async Task ClearFilters()
        {
            _search = null;
            _status = null;
            _source = null;
            await LoadLeads(true);
        }

        private async Task PreviousPage()
        {
            if (!CanGoPrevious) return;
            _pageNumber--;
            await LoadLeads();
        }

        private async Task NextPage()
        {
            if (!CanGoNext) return;
            _pageNumber++;
            await LoadLeads();
        }

        private string StatusText(LeadStatus status) => EnumService.Get(typeof(LeadStatus), status);

        private string SourceText(LeadSource source) => EnumService.Get(typeof(LeadSource), source);

        private string LeadStateClass(string statusText)
        {
            if (IsLeadStatusText(statusText, LeadStatus.New))
            {
                return "state-new";
            }

            if (IsLeadStatusText(statusText, LeadStatus.Contacted) || IsLeadStatusText(statusText, LeadStatus.Qualified))
            {
                return "state-active";
            }

            if (IsLeadStatusText(statusText, LeadStatus.Converted))
            {
                return "state-won";
            }

            if (IsLeadStatusText(statusText, LeadStatus.Lost) || IsLeadStatusText(statusText, LeadStatus.Disqualified))
            {
                return "state-lost";
            }

            return "state-new";
        }

        private bool IsLeadStatusText(string statusText, LeadStatus status)
            => string.Equals(statusText, StatusText(status), StringComparison.CurrentCultureIgnoreCase);
    }
}
