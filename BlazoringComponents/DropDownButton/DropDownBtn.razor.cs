using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.DropDownButton
{
    public partial class DropDownBtn: ComponentBase
    {
        [Parameter]
        public string Text { get; set; }

        [Parameter]
        public string IconMaterial { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string CssClass { get; set; } = "btn-secondary";
    }
}
