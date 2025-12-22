using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents
{
    public partial class BlazorBreadCrumb: ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Parameter]
        public List<BreadcrumbModel> Model { get; set; }

        [Parameter]
        public bool Visible { get; set; } = true;
        
        protected override void OnParametersSet()
        {
            StateHasChanged();
        }

        private async Task Goto(string url, Func<object, Task> action, object p)
        {
            if (action == null)
                NavigationManager.NavigateTo(url);
            else
                await action.Invoke(p);
        }
    }
}
