using AutoMapper.Configuration.Conventions;
using CNM.Authorize;
using CRM.Client.Pages.Settings.Users;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using TL;

namespace CRM.Server.Controllers
{
    [AuthorizeRole(ePolicy.ClientRole)]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly ApplicationDbContext _context;

        private readonly IPermitsService _permitsService;

        private readonly IEmailSenderPlus _emailSender;

        private readonly ILogEventService _logEventService;

        private readonly IArchiveService _archiveService;

       

        public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IPermitsService permitsService, IEmailSenderPlus emailSender, ILogEventService logEventService, IArchiveService archiveService)
        {
            _userManager = userManager;
            _context = context;
            _permitsService = permitsService;
            _emailSender = emailSender;
            _logEventService = logEventService;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Photo;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetUsers([FromQuery] UsersFilterModel args)
        {
            try
            {
                var users = _userManager.Users.Where(x=>!x.IsDeleted).Include(x => x.Company).AsQueryable();

               
                if (await _permitsService.IsClient())
                {
                    // Il Client puo' vedere solo gli utenti della sua azienda
                    args.IdCompany = await _permitsService.GetIdCompany();
                }
                if (args.IdCompany != null)
                    users = users.Where(x => x.IdCompany == args.IdCompany);

                if (args.IdCustomer != null)
                    users = users.Where(x => x.idCustomer == args.IdCustomer);

                if (args.Name != null && args.Name.Length > 0)
                    users = users.Where(x => x.Name.Contains(args.Name));

                if (args.SurName != null && args.SurName.Length > 0)
                    users = users.Where(x => x.Surname.Contains(args.SurName));

                if (args.Email != null && args.Email.Length > 0)
                    users = users.Where(x => x.Email.Contains(args.Email));

                if (args.AdminConfirmed == false)
                    users = users.Where(x => x.AdminConfirmed == false);

                if (args.IdGroup != null && args.IdGroup > 0)
                {
                    users = users.Where(x =>  x.Groups.Where(y => y.Id == args.IdGroup).Any());
                }

                 
                if (args.IdTicketType != null && args.IdTicketType > 0)
                    users = users.Where(x => x.TicketTypes.Where(y => y.Id == args.IdTicketType).Any() || x.Groups.Where(g=>g.TicketTypes.Where(t=>t.Id == args.IdTicketType).Any()).Any());

                if (args.IdTicketAssigned != null && args.IdTicketAssigned > 0)
                    users = users.Where(x => x.UserAssignedTickets.Where(t => t.Id == args.IdTicketAssigned).Any());

                if (args.NameComplete != null && args.NameComplete.Length > 0)
                {
                   
                    var fields = args.NameComplete.Split(" ");
                    foreach (var field in fields)
                    {
                        users = users.Where(x => x.Name.Contains(field) || x.Surname.Contains(field));
                        
                    }

                }

                if (args.IdTicketToAssign != null && args.IdTicketToAssign > 0)
                {
                    var usersTicket = await _permitsService.GetUsersCanAssignTicket(args.IdTicketToAssign.Value);
                    users = users.Where(x => usersTicket.Contains(x));
                }
                else if (args.TicketTypeToAssign != null && args.TicketTypeToAssign > 0)
                {
                    var usersTicket = await _permitsService.GetUsersCanAssignTicketType(args.TicketTypeToAssign.Value);
                    users = users.Where(x => usersTicket.Contains(x));
                }

                if (args.IdProject != null)
                {
                    users = users.Where(x=>x.Projects.Where(y=>y.IdProject == args.IdProject).Any());
                }     

                if (args.IdProjectParent != null)
                {
                    users = users.Where(x=>x.Projects.Where(x=>x.IdProject == args.IdProjectParent).Any() == false); 
                }

                if (args.Filter != null)
                {
                    users = users.Where(args.Filter);
                }

                int count = users.Count();
                int totalPage = 0;

                if (args.OrderBy != null && args.OrderBy.Length > 0)
                {
                    users = users.OrderBy(args.OrderBy);
                }
                else
                    users = users.OrderBy(x=>x.Surname).ThenBy(x=>x.Name);

                if (args.Skip != null && args.Top != null)
                {
                    users = users.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                else if (args.PageSize > 0)
                {
                    users = users.Skip((args.PageNumber - 1) * args.PageSize).Take(args.PageSize);
                    totalPage = (int)Math.Ceiling(count / (double)args.PageSize);
                }
                else
                {
                    totalPage = 1;

                }

                bool nextPage = args.PageNumber < totalPage;
                bool previousPage = args.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCount = count,
                    pageSize = args.PageSize,
                    currentPage = args.PageNumber,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                var list = await users.ToListAsync();

                
                
                return list;

            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(UsersController), nameof(GetUsers), LogEvent.EventsTypes.Error, ex);
                return new List<ApplicationUser>();
            }

        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApplicationUser>> GetUser(string id)
        {
            ApplicationUser? user = null;

            if (string.IsNullOrEmpty(id))
            {
                // Se l'ID è vuoto, restituisci l'utente corrente
                if (HttpContext?.User?.Identity != null)
                {
                    user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);
                    if (user != null)
                    {
                        user.Company = await _context.Companies.FindAsync(user.IdCompany);
                    }
                }
            }
            else
            {
                // Altrimenti restituisci l'utente richiesto
                user = await _userManager.Users.Where(x => x.Id == id).FirstOrDefaultAsync();
                if (user != null)
                {
                    user.Company = await _context.Companies.FindAsync(user.IdCompany);
                }
            }
            
            if (user == null)
                user = new ApplicationUser();

            user.Roles = (await _userManager.GetRolesAsync(user)).ToList();
            return user;
        }


        [HttpGet("Profile/{id}")]
        public async Task<ActionResult<UserModel>> GetProfile(string? id)
        {
            UserModel model = new UserModel();

            ApplicationUser? user = null;
            
            if (id != null)
                user = await _userManager.FindByIdAsync(id);
            else if (HttpContext.User?.Identity?.Name != null)
                user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);

            if (user != null)
            {
                model.Id = user.Id;
                model.AvatarTxt = @AvatarsHelper.AvatarTxt(user.Surname, user.Name);
                model.Color = user.Color;
                model.Email = user.Email;
                model.IdCompany = user.IdCompany;
                model.LanguageCode = user.LanguageCode;
                model.Name = user.Name;
                model.PhoneNumber = user.PhoneNumber;
                model.Surname = user.Surname;
                model.Photo =  await GetAvatar(user.Id);
                model.UserName = user.UserName;
                model.CompanyPreview = user.CompanyPreview;
                model.Enabled = user.Enabled;   
                model.AdminConfirmed = user.AdminConfirmed;
                model.IsDeleted = user.IsDeleted;

                var company = await _context.Companies.FindAsync(user.IdCompany);

                if (company != null)
                {
                    model.Company = company.RagioneSociale;
                }


            }
            return model;

        }

        [HttpGet("Profile")]
        public async Task<ActionResult<UserModel>> GetCurrentProfile()
        {
            UserModel model = new UserModel();

            ApplicationUser? user = null;

           
            if (HttpContext.User?.Identity?.Name != null)
                user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);

            if (user != null)
            {
                model.Id = user.Id;
                model.AvatarTxt = @AvatarsHelper.AvatarTxt(user.Surname, user.Name);
                model.Color = user.Color;
                model.Email = user.Email;
                model.IdCompany = user.IdCompany;
                model.LanguageCode = user.LanguageCode;
                model.Name = user.Name;
                model.PhoneNumber = user.PhoneNumber;
                model.Surname = user.Surname;
                model.Photo = await GetAvatar(user.Id);
                model.UserName = user.UserName;
                model.IsDeleted = user.IsDeleted;

                var company = await _context.Companies.FindAsync(user.IdCompany);

                if (company != null)
                {
                    model.Company = company.RagioneSociale;
                }


            }
            return model;

        }

        [HttpGet("CurrentUser")]
        public async Task<ActionResult<ApplicationUser>> GetCurrentUser()
        {
            return await GetUser("");
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponseModel>> PutUser(string id, UserModel model)
        {
            if (id != model.Id)
            {
                return new ApiResponseModel() { State = false, Message = "Errore id diversi" };
            }

            var user = await _userManager.Users.Where(x => x.Id == id).FirstOrDefaultAsync();
            
            if (user == null)
                return new ApiResponseModel() { State = false };

            user.IdCompany = model.IdCompany;
           
            user.Name = model.Name;
            user.PhoneNumber = model.PhoneNumber;
            user.Surname = model.Surname;
            user.Enabled = model.Enabled;
            user.AdminConfirmed = model.AdminConfirmed;
            user.Color = model.Color;
            
           
            try
            {
                await _userManager.UpdateAsync(user);

                await SetAvatar(user.Id, model.Photo);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!UserExists(id))
                {
                    return new ApiResponseModel() { State = false, Message = "Utente insesistente" };
                }
                else
                {
                    return new ApiResponseModel() { State = false, Message = ex.Message };
                }
            }

            return new ApiResponseModel() { State = true };
        }

        // POST: api/Customers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPost]
        public async Task<ActionResult<ApplicationUser>> PostUser(UserModel user)
        {
            try
            {
                if (_userManager.Users.Where(x => x.UserName == user.Email).Any())
                    return BadRequest(new ApiResponseModel() { State = false, Message = "Email Duplicate", Code = ErrorsHelper.ErrorEmailDuplicate });

                ApplicationUser appUser = new ApplicationUser()
                {
                    Email = user.Email,
                    IdCompany = user.IdCompany,
                    Name = user.Name,
                    Surname = user.Surname,
                    UserName = user.Email,
                    Enabled = user.Enabled,
                    Color = user.Color
                   
                };
                var identityResult = await _userManager.CreateAsync(appUser);

                
                if (identityResult.Succeeded)
                {
                    await _userManager.AddToRoleAsync(appUser, eRoles.Client.ToString());
                    await SetAvatar(appUser.Id, user.Photo);

                    
                }

                return appUser;
            }

           
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(UsersController), nameof(PostUser), LogEvent.EventsTypes.Error, ex.Message);
                return Problem(ex.Message, null, 500, "Error");
            }
            
        }

        // DELETE: api/Customers/5
        [HttpDelete("{id}")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.Users.Where(x => x.Id == id).FirstOrDefaultAsync();

               
                if (user == null)
                    return NotFound();

                user.UserName = $"{user.UserName}_deleted_{DateTime.Now.Ticks}";
                user.Email = $"{user.Email}_deleted_{DateTime.Now.Ticks}";
                user.IsDeleted = true;
                await _userManager.UpdateAsync(user);
               
                // Imposta roles a null
                var roles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, roles);



                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(UsersController), nameof(DeleteUser), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        
        [HttpGet("Confirm/{id}")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<ActionResult<ApplicationUser>> ConfirmUser(string id)
        {
            var user = await _userManager.Users.Where(x => x.Id == id).FirstOrDefaultAsync();

            if (user == null || user.IsDeleted)
                return NotFound();

            user.AdminConfirmed = true;
            user.Enabled = true;
            await _userManager.UpdateAsync(user);

           
            var callbackUrl = Url.Page(
               "/Account/Login",
                    pageHandler: null,
                    values: new { area = "Identity" },
                    protocol: Request.Scheme);

            Dictionary<string, string> keyValues = new Dictionary<string, string>() { { EmailHelper.KeyWord(EmailHelper.KeyWords.Name), user.Name }, { EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl ?? "" } };
            await _emailSender.SendEmailAsync(user.Email, EmailsTypes.ConfirmRegister, null, keyValues);

            return user;
        }

        [HttpGet("SendInvite/{id}")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task <ActionResult<bool>> SendInvite(string id)
        {
            try
            {
                var user = await _userManager.Users.Where(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

                if (user == null)
                    return NotFound();

                user.WaitConfirmInvite = true;
                await _userManager.UpdateAsync(user);

               
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/Invite",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code = code },
                    protocol: Request.Scheme);

                Dictionary<string, string> keyValues = new Dictionary<string, string>();

                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), user.Name);
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl ?? "");


                await _emailSender.SendEmailAsync(user.Email, EmailsTypes.Invito, null, keyValues);

                user.DateSendInvite = DateTime.Now;

                await _userManager.UpdateAsync(user);

                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(UsersController), nameof(SendInvite), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }

        [HttpGet("checkpolicy/{policy}")]
        public async Task<bool> CheckPolicy(int policy)
        {
            var resp = await _permitsService.CheckPolicy((ePolicy)policy);

            return resp.Succeeded;
        }

        [HttpGet("companytype")]
        public async Task<int> GetCompanyType()
        {
            var company = await _permitsService.GetCompany();

            return (int) (company?.CompanyType ?? CompanyTypes.Customer);
        }

        private bool UserExists(string id)
        {
            return _userManager.Users.Where(x => x.Id == id && !x.IsDeleted).Any();
        }

        private async Task<string?> GetAvatar(string idUser)
        {
            var avatar = await _context.UserAvatars.FirstOrDefaultAsync(x=>x.IdUser == idUser);

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

        // POST: api/Customers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpGet("disable/{id}")]
        public async Task<ActionResult<bool>> Disable(string id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user != null)
                {
                    user.Enabled = false;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;

            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(UsersController), nameof(Disable), LogEvent.EventsTypes.Error, ex.Message);
                return Problem(ex.Message, null, 500, "Error");
            }

        }
    }
}
