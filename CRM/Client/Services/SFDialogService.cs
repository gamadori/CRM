using FluentValidation;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class SFDialogService
    {
        private readonly DialogService _dialogService;

        private readonly IStringLocalizer<CRM.Shared.Resources.App> _localize;
        public SFDialogService(DialogService dialogService, IStringLocalizer<CRM.Shared.Resources.App> localize)
        {
            _dialogService = dialogService;
            _localize = localize;
        }

        public async Task<bool> Confirm(string message, string header)
        {
            var resp = await _dialogService.Confirm(message, header, new ConfirmOptions() { CancelButtonText = _localize["No"], OkButtonText = _localize["Si"] });
            return resp.GetValueOrDefault();
        }

        public async Task Message(string message, string header)
        {
            await _dialogService.Alert(message, header, new AlertOptions() { OkButtonText = _localize["OK"] });        
        
        }
        public DialogService DialogService
        {
            get { return _dialogService; }
        }

    }
}
