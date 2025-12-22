using BlazoringComponents.Models;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGTicketOthersScheduler: ComponentBase
    {
       
        [Parameter]
        public DateTime Day { get; set; }

        [Parameter]
        public string Desc { get; set; }

        [Parameter]
        public Func<DateTime, Task> OpenDaylyScheduler { get; set; }

    }
}
