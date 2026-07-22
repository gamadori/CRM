using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CRM.Server.Areas.Identity.Pages.MainCompany
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser> _userManager;

        [ViewData]
        public bool CompaniesEmpty { get; set; }

        [BindProperty]
        public Company Company { get; set; }

        public RegisterModel(
          ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            Company = new Company();
        }

        public IActionResult OnGet()
        {
            if (!_context.Companies.Any())
                return Page();

            if (!_userManager.Users.Any())
            {
                return RedirectToPage(Url.Content("/Account/Register"));
            }
            else
                return RedirectToPage(Url.Content("~/"));

        }

        public async Task<IActionResult> OnPostAsync()
        {

            if (ModelState.IsValid)
            {
                // Primo avvio: la società registrata qui è l'azienda che opera il CRM.
                // Nasce già come HeadCompany, unica fonte di verità per branding e permessi.
                Company.CompanyType = CompanyTypes.HeadCompany;

                _context.Companies.Add(Company);

                await _context.SaveChangesAsync();
                return RedirectToPage(Url.Content("/Account/Register"));
            }
            return Page();
        }
    }
}
