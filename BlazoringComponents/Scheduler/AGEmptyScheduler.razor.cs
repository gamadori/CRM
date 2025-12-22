using BlazoringComponents.Helpers;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGEmptyScheduler : ComponentBase
    {
        [Parameter]
        public bool IsHoliday { get; set; }

        private string _bgHeader;

        protected override void OnParametersSet()
        {
            _bgHeader = DayHelper.GetBgHeader(IsHoliday);
            base.OnParametersSet();
        }
    }
}
