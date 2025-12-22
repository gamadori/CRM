using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class ArticlesDetails : ComponentBase
    {
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public List<TicketInterventionArticleModel> Articles { get; set; }

        private RadzenDataGrid<TicketInterventionArticleModel> _articlesGrid;


       
    }
}
