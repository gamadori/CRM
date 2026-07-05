using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Resources.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Deals
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        private IDealService DealService { get; set; } = default!;

        [Inject]
        private ICompaniesService CompaniesService { get; set; } = default!;

        [Inject]
        private IContactsService ContactsService { get; set; } = default!;

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        private DialogService DialogService { get; set; } = default!;

        [Inject]
        private IEnumService EnumService { get; set; } = default!;

        [Inject]
        private IHeaderService HeaderService { get; set; } = default!;

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdParent { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public Action? OnClickSave { get; set; }

        [Parameter]
        public Action? OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private DealDTO? _deal;
        private List<CompanyDTO> _companies = new();
        private List<ContactDTO> _contacts = new();
        private List<EnumField> _dealPhases = new();
        private List<EnumField> _dealStates = new();
        private bool _loading = true;
        private bool _saving;
        private bool _lockCompany;
        private string? _messageState;
        private PageHeaderModel? _pageHeader;
        private string Header => Id == null ? "Nuova opportunita" : "Modifica opportunita";

        protected override async Task OnInitializedAsync()
        {
            if (OnClickSave == null)
                _pageHeader = await HeaderService.Create();

            _dealStates = EnumService.EnumGetList(typeof(DealStates));
            _dealPhases = EnumService.EnumGetList(typeof(DealPhases));
            await LoadCompanies();
            await LoadDeal();
        }

        private async Task LoadDeal()
        {
            _loading = true;
            try
            {
                if (Id != null)
                {
                    _deal = await DealService.GetItemAsync(Id.Value);
                }
                else
                {
                    _deal = new DealDTO
                    {
                        Date = DateTime.Today,
                        DateClosed = default,
                        IdCompany = IdCompany,
                        State = DealStates.Open,
                        Phase = DealPhases.InitialContact
                    };

                    _lockCompany = IdCompany != null;
                }

                await LoadContacts();
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task LoadCompanies()
        {
            var items = await CompaniesService.GetListAsync(new CompanyFilter());
            _companies = items ?? new List<CompanyDTO>();
        }

        private async Task LoadContacts()
        {
            if (_deal?.IdCompany == null)
            {
                _contacts = new List<ContactDTO>();
                return;
            }

            var response = await ContactsService.GetListAsync(new ContactFilter { IdCompany = _deal.IdCompany });
            _contacts = response ?? new List<ContactDTO>();

            if (_deal.IdContact != null && !_contacts.Any(x => x.Id == _deal.IdContact))
            {
                _deal.IdContact = null;
            }
        }

        private async Task OnChangeCompany()
        {
            if (_deal != null)
            {
                _deal.IdContact = null;
            }

            await LoadContacts();
        }

        private async Task OnChangeContact()
        {
           
        }

        private async Task HandleValidSubmit()
        {
            if (_deal == null)
            {
                return;
            }

            _saving = true;
            _messageState = null;
            try
            {
                var response = await DealService.PostAsync(_deal.ToEntity());
                if (response.State)
                {
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(response.Data?.Id ?? _deal.Id);
                    }
                    else if (OnClickSave != null)
                    {
                        OnClickSave();
                    }
                    else
                    {
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{response.Data?.Id ?? _deal.Id}");
                    }
                }
                else
                {
                    _messageState = response.Message;
                }
            }
            finally
            {
                _saving = false;
            }
        }

        private void Cancel()
        {
            if (OnClickCancel != null)
            {
                OnClickCancel();
            }
            else if (Id != null)
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{Id}");
            }
            else
            {
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}");
            }
        }

        private string StateText(object value)
        {
            return value is DealStates state ? EnumService.Get(typeof(DealStates), state) : string.Empty;
        }

        private string PhaseText(object value)
        {
            return value is DealPhases phase ? EnumService.Get(typeof(DealPhases), phase) : string.Empty;
        }

        private async Task OnGetCompany(object? id)
        {
            if (id != null)
            {
                await LoadCompanies();

                StateHasChanged();
                _deal.IdCompany = (int)id;
                StateHasChanged();

            }
        }

        private async Task OnGetContact(object? id)
        {
            if (id != null)
            {
                await LoadContacts();

                StateHasChanged();
                _deal.IdContact = (int)id;
                StateHasChanged();

            }
        }
    }
}
