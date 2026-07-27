#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using CRM.Shared;
using Microsoft.Extensions.Localization;
using CRM.Server.Services;
using CRM.Server.Helpers;

namespace CRM.Server.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderPlus _emailSender;
        private readonly IStringLocalizer<CRM.Shared.Resources.App> _localizer;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSenderPlus emailSender, IStringLocalizer<CRM.Shared.Resources.App> localizer)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _localizer = localizer;

        }

        [BindProperty]
        public InputModel Input { get; set; }

  
        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                

                // For more information on how to enable account confirmation and password reset please 
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);
                
                var keyValues = new Dictionary<string, string>
                {
                    { EmailHelper.KeyWord(EmailHelper.KeyWords.Name), user.NameComplete ?? user.UserName ?? string.Empty },
                    { EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl ?? string.Empty },
                    { EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g") }
                };

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    EmailsTypes.PasswordReset,
                    null,
                    keyValues,
                    culture: user.LanguageCode);

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
