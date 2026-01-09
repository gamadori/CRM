
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Languages
{
    public partial class Index : ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        DialogService DialogService { get; set; } = default!;   

        [Inject]
        ILanguagesService LanguagesService { get; set; } = default!;

        [Inject]
        IHeaderService HeaderService { get; set; } = default!;

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private const string PageFolder = "Settings/Languages";

        private List<Language>? _languages;

        private LanguageFilter _filter = new LanguageFilter();

        private int _totalCount = 0;

        private RadzenDataGrid<Language> _grdLanguages = default!;

        private bool _loading = true;

        private List<Language> _languageToUpdate = new List<Language>();

        private List<Language> _languagesToInsert = new List<Language>();

        private PageHeaderModel? _pageHeader = null;

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

            var resp = await LanguagesService.GetPagingAsync(_filter);

            _languages = resp?.Items ?? new List<Language>();
            _totalCount = resp?.MetaData?.TotalCount ?? 0;
            _loading = false;

            _pageHeader = await HeaderService.Create(PageMode);
            StateHasChanged();
        }

      


        private async Task Delete(Language item)
        {
            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente la lingua"]}: {item.Name}") == true)
            {
                await LanguagesService.DeleteAsync(item.Id);

                await LoadData();
            }
        }

        async Task EditRow(Language language)
        {
            if (!_grdLanguages.IsValid) 
                return;
    
            Reset();
            
            _languageToUpdate.Add(language);
            await _grdLanguages.EditRow(language);
        }

        private async Task OnUpdateRow(Language language)
        {
            Reset(language);

            var resp = await LanguagesService.PostAsync(language);

            await _grdLanguages.Reload();

        }

        private void CancelEdit(Language language)
        {
            Reset(language);

            _grdLanguages.CancelEditRow(language);

        }

        private async Task DeleteRow(Language item)
        {
            Reset(item);
            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente la linua"]}: {item.Name}?") != true)
            {
                _grdLanguages.CancelEditRow(item);
                return;
            }
            if (_languages != null && _languages.Contains(item))
            {
                await LanguagesService.DeleteAsync(item.Id);

                await _grdLanguages.Reload();
            }
            else
            {
                _grdLanguages.CancelEditRow(item);
                await _grdLanguages.Reload();
            }
        }

        async Task InsertRow()
        {
            if (!_grdLanguages.IsValid)
                return;

            Reset();

            var language = new Language();
            
            _languagesToInsert.Add(language);
            await _grdLanguages.InsertRow(language);
            
        }

        async Task InsertAfterRow(Language row)
        {
            if (!_grdLanguages.IsValid) return;
            Reset();

            var language = new Language();
            _languagesToInsert.Add(language);
            await _grdLanguages.InsertAfterRow(language, row);
        }

        async Task OnCreateRow(Language language)
        {
            var resp = await LanguagesService.PostAsync(language);
            Reset(language);
            await   _grdLanguages.Reload();
        }



        void Reset(Language language)
        {
            _languagesToInsert.Remove(language);
            _languageToUpdate.Remove(language);
        }


        private void Reset()
        {
           
            _languageToUpdate.Clear();
            _languageToUpdate.Clear();
        }

        

        private async Task SaveRow(Language language)
        {
            if (!_grdLanguages.IsValid)
                return;
            await _grdLanguages.UpdateRow(language);
        }




        private void OnChange(string value, string name)
        {
        }

        private void OnError(UploadErrorEventArgs args, string name)
        {
        }




    }
}
