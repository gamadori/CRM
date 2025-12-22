using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public class ApplicationSignInManager<TUser> : SignInManager<TUser> where TUser : class
    {

        private readonly UserManager<TUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _contextAccessor;

        public ApplicationSignInManager(UserManager<TUser> userManager, IHttpContextAccessor contextAccessor, IUserClaimsPrincipalFactory<TUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor, ILogger<SignInManager<TUser>> logger, ApplicationDbContext dbContext, IAuthenticationSchemeProvider schemes, IUserConfirmation<TUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
            _db = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public override async Task<SignInResult> PasswordSignInAsync(TUser user, string password, bool isPersistent, bool lockoutOnFailure)
        {
            var appUser = user as ApplicationUser;
            if (appUser != null)
            {
                if (appUser.IsDeleted)
                    return SignInResult.Failed;
                if (!appUser.Enabled)
                    return SignInResult.LockedOut;
                else if (!appUser.AdminConfirmed)
                    return SignInResult.NotAllowed;
            }

            var result = await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);



            return result;
        }
    }
}
