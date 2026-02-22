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
    public partial class Details : ComponentBase
    {
        [Parameter]
        public int Id { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IFoldersService Service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        private FolderDTO _folder;
        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadFolder();
            await base.OnInitializedAsync();
        }

        private async Task LoadFolder()
        {
            try
            {
                _isLoading = true;
                _folder = await Service.GetItemAsync(Id);
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

        private void GoToEdit()
        {
            NavigationManager.NavigateTo($"Settings/Folders/Edit/{Id}");
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