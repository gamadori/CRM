using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CRM.Server.Areas.Identity.Pages.Account
{
    // Landing di conferma registrazione: raggiunta da utente non autenticato.
    [AllowAnonymous]
    public class ConfirmMasterModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
