using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Folders
{
    public partial class Edit : ComponentBase
    {
        [Parameter]
        public int? Id { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IFoldersService Service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        private Folder _folder = new Folder();
        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            if (Id.HasValue && Id.Value > 0)
            {
                await LoadFolder();
            }
            await base.OnInitializedAsync();
        }

        private async Task LoadFolder()
        {
            try
            {
                _isLoading = true;
                var dto = await Service.GetItemAsync(Id.Value);
                if (dto != null)
                {
                    _folder = new Folder
                    {
                        Id = dto.Id,
                        Name = dto.Name,
                        Description = dto.Description
                    };
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                Notify(ex.Message, NotificationSeverity.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task OnSubmit()
        {
            try
            {
                _isLoading = true;
                var resp = await Service.PostAsync(_folder);

                if (resp != null && resp.State)
                {
                    Notify(Localize["SavedData"], NotificationSeverity.Success);
                    NavigationManager.NavigateTo("Settings/Folders");
                }
                else
                {
                    Notify(resp?.Message ?? "Error", NotificationSeverity.Error);
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                Notify(ex.Message, NotificationSeverity.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void GoBack()
        {
            NavigationManager.NavigateTo("Settings/Folders");
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }
    }
}