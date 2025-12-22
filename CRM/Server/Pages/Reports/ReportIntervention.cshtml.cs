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
using System.Globalization;
using NuGet.Common;

namespace CRM.Server.Pages.Reports
{
    public class ReportInterventionModel : PageModel
    {
        private readonly CRM.Server.Data.ApplicationDbContext _context;
        private IStringLocalizer<Reports.ReportInterventionModel> _localize;

        
        public ReportInterventionModel(CRM.Server.Data.ApplicationDbContext context, IStringLocalizer<ReportInterventionModel> localize)
        {
            _context = context;
            ListInterventionTypes = new List<Dictionary<string, bool>>();
            _localize = localize;
        }

        public TicketIntervention? TicketIntervention { get; set; }

        public Company? Company { get; set; }

        public TicketInterventionProductModel? Product { get; set; }

        public  List<Dictionary<string, bool>> ListInterventionTypes { get; set; }


        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var cultureInfo = new CultureInfo("en");
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");


            
            string prova = _localize["prova"];

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            prova = _localize["prova"];

            int numCols = 4;

            if (id == null)
            {
                return NotFound();
            }

            TicketIntervention = await _context.TicketsInterventions
                .Include(t => t.Ticket).ThenInclude(t=>t.Company)
                .Include(t=>t.Ticket.Article)
                .Include(t=>t.TicketInterventionsTypes)
                .Include(t => t.User).FirstOrDefaultAsync(m => m.Id == id);


            if (TicketIntervention == null)
            {
                return NotFound();
            }

            Company = TicketIntervention.Ticket.Company;

            ListInterventionTypes = new List<Dictionary<string, bool>>();
            Dictionary<string, bool> row = null;

            foreach (var item in _context.InterventionTypes)
            {
                if (row == null || row.Count() % numCols == 0)
                {
                    if (row != null && row.Count() > 0)
                    {
                        ListInterventionTypes.Add(row);
                    }
                    row = new Dictionary<string, bool>();       
                }
                bool value = TicketIntervention.TicketInterventionsTypes.Where(x => x.Id == item.Id).Any();
                row.Add(item.Name, value);
            }
            if (row != null && row.Count() > 0)
                ListInterventionTypes.Add(row);

           
            Product = new TicketInterventionProductModel();

            var product = TicketIntervention.Ticket.Article;

            if (product != null)
            {
                Product.Selected = true;
                Product.SerialNumber = product.SerialNumber;
                Product.Year = product.Year.ToString();
                    
            }

            return Page();
        }
    }
}
