#nullable disable

using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserSignedController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly ApplicationDbContext _context;

        private readonly IPermitsService _permitsService;

        public UserSignedController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IPermitsService permitsService)
        {
            _userManager = userManager;
            _context = context;
            _permitsService = permitsService;
        }

        [HttpGet]
        public async Task<ActionResult<ApplicationUser>> Get()
        {

            ApplicationUser user;

            user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);
       
            user.Roles = (await _userManager.GetRolesAsync(user)).ToList();

            user.Company = await _context.Companies.FindAsync(user.IdCompany);
            user.AvatarTxt = AvatarsHelper.AvatarTxt(user.Surname, user.Name);

            user.CanManageOtherCompany = await _permitsService.CanAccessOtherCompany();

            user.CanTicketAssign = user.Roles.Contains(eRoles.Admin.ToString()) || user.Roles.Contains(eRoles.SuperUser.ToString())
                || user.Roles.Contains(eRoles.Standard.ToString());
            
            user.IsClient = await _permitsService.IsClient();

            user.IsAdmin = await _permitsService.IsAdmin();

            user.IsSuperUser = await _permitsService.IsSuperUser();

            user.ArticlePermits = await _permitsService.ArticlePermits();

            user.CompanyPermits = await _permitsService.CompanyPermits();

            user.ProductPermits = await _permitsService.ProductPermits();



            user.TicketInterventionPermits = await _permitsService.ObjectPermits(user.IdCompany, null);

            return user;
        }

        [HttpGet("CanAttachmentDelete/{id}")]
        public async Task<ActionResult<bool>> CanAttachmentDelete(int id)
        {
            return await _permitsService.CanDeleteAttachment(id);
        }

        //// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        //[HttpPost("Profile")]
        //public async Task<ActionResult<ApiResponseModel>> Profile(ApplicationUser user)
        //{
        //    ApplicationUser userCurrent;

        //    userCurrent = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);

        //    if (userCurrent == null)
        //    {
        //        return new ApiResponseModel() { State = false, Message = "Utente non loggato" };
        //    }
        //    else if (userCurrent.Id != user.Id)
        //    {
        //        return new ApiResponseModel() { State = false, Message = "Utente non valido" };
        //    }
        //    userCurrent.LanguageCode = user.LanguageCode;
        //    userCurrent.Photo = user.Photo;

        //    try
        //    {
        //        await _userManager.UpdateAsync(userCurrent);
        //    }
        //    catch (DbUpdateConcurrencyException ex)
        //    {
        //        return new ApiResponseModel() { State = false, Message = ex.Message };

        //    }

        //    return new ApiResponseModel() { State = true };
        //}
        [HttpGet("Profile")]
        public async Task<ActionResult<UserModel>> GetProfile()
        {
            UserModel model = new UserModel();

            var user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);

            if (user != null)
            {
                model.Id = user.Id;
                model.AvatarTxt = user.AvatarTxt;
                model.Color = user.Color;
                model.Email = user.Email;
                model.IdCompany = user.IdCompany;
                model.LanguageCode = user.LanguageCode;
                model.Name = user.Name;
                model.PhoneNumber = user.PhoneNumber;                
                model.Surname = user.Surname;
                model.Photo = await GetAvatar(user.Id);
                model.AdminConfirmed = user.AdminConfirmed;
                model.Enabled = user.Enabled;

                var company = await _context.Companies.FindAsync(user.IdCompany);

                if (company != null)
                {
                    model.Company = company.RagioneSociale;
                }
                

            }
            return model;

        }

        [HttpPost("Profile")]
        public async Task<ActionResult<ApiResponseModel>> Profile(UserModel user)
        {
            ApplicationUser userCurrent;

            userCurrent = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);

            if (userCurrent == null)
            {
                return new ApiResponseModel() { State = false, Message = "Utente non loggato" };
            }
            else if (userCurrent.Id != user.Id)
            {
                return new ApiResponseModel() { State = false, Message = "Utente non valido" };
            }
            userCurrent.LanguageCode = user.LanguageCode;
            
            try
            {
                await _userManager.UpdateAsync(userCurrent);
                await SetAvatar(userCurrent.Id, user.Photo);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return new ApiResponseModel() { State = false, Message = ex.Message };

            }

            return new ApiResponseModel() { State = true };
        }

        [HttpPost("Language")]
        public async Task<IActionResult> PostLnguage([FromBody]string language)
        {
            string idUser = await _permitsService.IdUser();

            ApplicationUser userCurrent;

            userCurrent = await _userManager.FindByIdAsync(idUser);

            if (userCurrent != null)
            {
                userCurrent.LanguageCode = language;
                await _userManager.UpdateAsync(userCurrent);
            }
            return NoContent();
        }

        private async Task<string?> GetAvatar(string idUser)
        {
            var avatar = await _context.UserAvatars.FirstOrDefaultAsync(x => x.IdUser == idUser);

            if (avatar != null)
                return avatar.Avatar;
            else
                return null;
        }

        private async Task SetAvatar(string idUser, string image)
        {
            var avatar = await _context.UserAvatars.FirstOrDefaultAsync(x => x.IdUser == idUser);

            if (avatar == null)
            {
                avatar = new UserAvatar() { IdUser = idUser };
                _context.UserAvatars.Add(avatar);
            }
            avatar.Avatar = image;
            await _context.SaveChangesAsync();
        }
    }
}
