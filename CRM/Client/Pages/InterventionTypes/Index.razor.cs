using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Client.Pages.InterventionTypes
{
    public partial class Index : ComponentBase
    {
        
        [Inject]
        NavigationManager NavigationManager { get; set; }

       
        [Inject]
        IInterventionTypesService Service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        SFDialogService DialogService { get; set; }   

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        private InterventionTypeFilter _filter = new InterventionTypeFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private RadzenDataGrid<InterventionType> _interventionGrid;

        private List<InterventionType> _interventions;

        InterventionType _interventionType;

        private PagingHeaderModel _paging = new PagingHeaderModel();


        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            await GetInterventions();
            await base.OnInitializedAsync();
        }

        private async Task GetInterventions(LoadDataArgs args = null)
        {
            try
            { 
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                }
                
                PagingResponse<InterventionTypeDTO> pagingResponse = await Service.GetPagingAsync(_filter);

                if (pagingResponse != null)
                {
                    _interventions = pagingResponse.Items.Select(i => new InterventionType
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Description = i.Description
                    }).ToList();
                    _paging = pagingResponse.MetaData;
                }
                else
                    Notify("Error", NotificationSeverity.Error);



            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {

                Notify(ex.Message, NotificationSeverity.Error);

            }

            catch (Exception ex)
            {
                Notify(ex.Message, NotificationSeverity.Error);

            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        async Task EditRow(InterventionType interventionType)
        {
            await _interventionGrid.EditRow(interventionType);
        }

        async Task OnUpdateRow(InterventionType item)
        {
            if (item == _interventionType)
            {
                _interventionType = null;
            }
            var resp = await Service.PostAsync(item);

            if (resp != null && !resp.State)
            {
                Notify(resp.Message, NotificationSeverity.Error);

            }
            else
                Notify(Localize["UpdatedData"], NotificationSeverity.Success);


        }

        private async Task SaveRow(InterventionType item)
        {
            if (item == _interventionType)
            {
                _interventionType = null;
            }

            await _interventionGrid.UpdateRow(item);

            await GetInterventions();
        }

        private async Task CancelEdit(InterventionType item)
        {
            if (item == _interventionType)
            {
                _interventionType = null;
            }

            _interventionGrid.CancelEditRow(item);

           await Service.PostAsync(item);
        }

        async Task DeleteRow(InterventionType item)
        {
            if (await DialogService.Confirm(Localize["DeleteIntervention?"], Localize["Delete"]))
            {
                if (item == _interventionType)
                {
                    _interventionType = null;
                }

                await Service.DeleteAsync(item.Id);
                await GetInterventions();
            }
        }

        async Task LanguageRow(InterventionType item)
        {
            if (item == _interventionType)
            {
                _interventionType = null;
            }

            NavigationManager.NavigateTo($"Settings/InterventionTypeLanguages/Index/{item.Id}");


        }


        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }



        async Task InsertRow()
        {
            _interventionType = new InterventionType();
            await _interventionGrid.InsertRow(_interventionType);
        }

        async void OnCreateRow(InterventionType item)
        {
            await Service.PostAsync(item);

            await GetInterventions();
            
        }
    }
}
