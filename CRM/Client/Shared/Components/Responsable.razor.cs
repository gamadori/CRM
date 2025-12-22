using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace CRM.Client.Shared.Components
{

    public partial class Responsable : ComponentBase
    {

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        private bool _isResponsable = false;
        [Parameter]
        public bool Value
        {
            get { return _isResponsable; }
            set
            {
                if (_isResponsable != value)
                {
                    _isResponsable = value;
                    ValueChanged.InvokeAsync(_isResponsable);
                }
            }
        }

        [Parameter]
        public EventCallback<bool> ValueChanged { get; set; }
    }
}
