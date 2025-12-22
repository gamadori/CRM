using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace CRM.Client.Shared
{
    public partial class NotAuthorized: ComponentBase
    {
        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }
    }
}
