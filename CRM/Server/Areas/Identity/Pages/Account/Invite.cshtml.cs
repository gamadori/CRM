#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CRM.Server.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class InviteModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public InviteModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            
            public string Code { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string userId, string code = null)
        {
            if (code == null)
            {
                return BadRequest("A code must be supplied for password reset.");
            }
            else
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    return NotFound($"Unable to load user with ID '{userId}'.");
                }

                Input = new InputModel
                {
                    Email = user.Email,
                    Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
                    Password = "",
                    ConfirmPassword = ""
                };
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                var user = await _userManager.FindByNameAsync(Input.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return NotFound($"Utente non trovato");
                }
                

                var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
                user.WaitConfirmInvite = false;
                user.EmailConfirmed = true;
                user.DateAcceptInvite = DateTime.Now;

                await _userManager.UpdateAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                if (result.Succeeded)
                {

                    return RedirectToPage($"./InviteConfirmation", new { userId = user.Id, code = code });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
            catch (Exception ex)
            {
                return NotFound($"User Error");
            }
        }
    }
}
