using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.Extensions.Localization;
using Duende.IdentityServer.Models;
using Syncfusion.Blazor.Internal;
using NuGet.Common;
using System.Globalization;

namespace CRM.Server.Pages.Reports
{
    public class InterventionModuleModel : PageModel
    {
        private readonly CRM.Server.Data.ApplicationDbContext _context;


        public InterventionModuleModel(CRM.Server.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public TicketIntervention TicketIntervention { get; set; } = default!; 

        public int IdLanguage { get; set; }

        public List<IterventionTypeReport>? InterventionTypes { get; set; } 

        public List<TicketInterventionArticleModel>? InterventionArticles { get; set; }
        public async Task<IActionResult> OnGetAsync(int? id, int idLanguage)
        {
            

            if (id == null || _context.TicketsInterventions == null)
            {
                return NotFound();
            }

            IdLanguage = idLanguage;

            var ticketintervention = await _context.TicketsInterventions
                .Include(x=>x.Ticket).ThenInclude(x=>x.Company)
                .Include(x=>x.TicketInterventionsTypes)
                .Include(x=>x.User)
                
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ticketintervention == null)
            {
                return NotFound();
            }
            else 
            {
                TicketIntervention = ticketintervention;
            }

            InterventionTypes = await _context.InterventionTypes.Select(x=>new IterventionTypeReport (){ Id = x.Id}).ToListAsync();

            foreach (var item in InterventionTypes)
            {
                item.Checked = (ticketintervention.TicketInterventionsTypes.Where(x => x.Id == item.Id).Any());
                item.Desc = ListToString(_context.InterventionTypeLanguages.Where(x => x.IdInterventionType == item.Id).OrderBy(x=>x.Language.Index).Select(x=>x.Name).Take(2).ToList());
            }
            InterventionArticles = _context.TicketInterventionArticles.Where(x => x.IdTicketIntervention == id).Select(x => new TicketInterventionArticleModel() { IdLink = x.Id,
                Year = x.Article != null ? x.Article.Year.ToString(): "",
                Article = x.Article != null ? x.Article.SerialNumber : "",
                Product = x.Product != null ? x.Product.Name : "" }).ToList();
            return Page();
        }

        private string ListToString(List<string> list)
        {
            string s = "";
            int l = list.Count();

            if (l > 0)
            {
                s = list[0];

                if (l > 1)
                    s += $" / {list[1]}";
            }
            return s;
        }
    }

    
}

