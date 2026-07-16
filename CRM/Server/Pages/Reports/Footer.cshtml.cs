using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CRM.Server.Pages.Reports
{
    // Letta via HTTP dal convertitore HTML->PDF, che non invia cookie di autenticazione.
    [AllowAnonymous]
    public class FooterModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
