using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Searching
{
    public partial class AGSearch: ComponentBase
    {
        [Parameter] public Action<bool> OnChangeFilter { get; set; }
        [Parameter] public RenderFragment Filter { get; set; }

        [Parameter]
        public EventCallback<bool> FilterStateChanged { get; set; }


        
    }
}
