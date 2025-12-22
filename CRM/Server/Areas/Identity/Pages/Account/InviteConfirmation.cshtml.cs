#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CRM.Server.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class InvitedConfirmationModel : PageModel
    {

        private readonly UserManager<ApplicationUser> _userManager;

        public InvitedConfirmationModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [ViewData]
        public string StatusMessage { get; set; }

        [ViewData]
        public bool Status { get; set; }

        public async Task<IActionResult> OnGet(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Utente non trovato: '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);
            
            Status = result.Succeeded;

            StatusMessage = result.Succeeded ? "Registrazione Cpompletata con successo" : "Si è verificato un errore durante la conferma della tua email.";
            return Page();
        }
    }
}
