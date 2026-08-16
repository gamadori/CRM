using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Groups
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        /// <summary>
        /// Da dove si e' arrivati alla pagina: "info" per la scheda del gruppo, altrimenti l'elenco.
        /// Serve a riportare l'utente al punto di partenza dopo il salvataggio.
        /// </summary>
        [SupplyParameterFromQuery(Name = "from")]
        public string From { get; set; }

        [Parameter]
        public EventCallback OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private Group _group = null;

        private string _messageState = null;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                if (Id != null)
                {
                    _group = await RestClientService.GetItem<Group, int>(Id.Value, ConstHelper.GroupsPath);
                }
                else
                {
                    _group = new Group();
                }
                _pageHeader = await HeaderService.Create(PageMode);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                // Il secondo tipo e' quello dell'Id: con un tipo diverso la lettura fallisce,
                // l'Id risulta assente e la modifica di un gruppo esistente parte come creazione.
                var resp = await RestClientService.Post<Group, int>(_group, ConstHelper.GroupsPath);

                if (resp != null && resp.State)
                {
                    if (OnClickSave.HasDelegate)
                        await OnClickSave.InvokeAsync();
                    else
                        NavigationManager.NavigateTo(ReturnUrl);
                }
                else
                    _messageState = "Errore durante il salvataggio";
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo(ReturnUrl);
        }

        /// <summary>
        /// Si torna alla scheda del gruppo se si e' arrivati da li', all'elenco in tutti gli altri casi.
        /// </summary>
        private string ReturnUrl => Id != null && string.Equals(From, "info", StringComparison.OrdinalIgnoreCase)
            ? $"/Settings/Groups/{Id}/Info"
            : "/Settings/Groups";

        void Change(string value, string name)
        {

        }

        void Error(Radzen.UploadErrorEventArgs args, string name)
        {

        }

    }
}
