using Microsoft.AspNetCore.Components;

namespace CRM.Client.Shared.Components
{
    public partial class NotFound: ComponentBase
    {
        [Parameter]
        public string Text { get; set; }
        protected override void OnInitialized()
        {
            base.OnInitialized();
        }
    }
}
