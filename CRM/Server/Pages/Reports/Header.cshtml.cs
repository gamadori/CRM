using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;

namespace CRM.Server.Pages.Reports
{
    public class HeaderModel : PageModel
    {
        private readonly CRM.Server.Data.ApplicationDbContext _context;

        public HeaderModel(CRM.Server.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public Company Company { get; set; }

        public string? Logo { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Company = await _context.Companies.FirstOrDefaultAsync(m => m.Id == id);

            var idLogo = (await _context.GlobalSettings.FirstOrDefaultAsync())?.LogoReport;

            if (idLogo != null)
            {
                Logo = (await _context.Logos.FirstAsync(x=>x.Id == idLogo))?.InputFile;
            }

            if (Company == null)
            {
                return NotFound();
            }
            return Page();
        }
    }
}
