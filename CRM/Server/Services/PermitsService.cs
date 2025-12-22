#nullable disable

using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto;
using Syncfusion.Blazor.DropDowns;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TL;
using TL.Methods;

namespace CRM.Server.Services
{
    public class PermitsService: IPermitsService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private IAuthorizationService _authorization;

        public PermitsService(ApplicationDbContext  context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, 
            IHttpContextAccessor httpContextAccessor,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
            _authorization = authorizationService;
            
        }

        /// <summary>
        /// Id dell'utente corrente
        /// </summary>
        /// <returns></returns>
        public async Task<string> IdUser()
        {
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;

            if (userName == null)
                return "";

            var user = await _userManager.FindByNameAsync(userName);

            if (user != null)
                return user.Id;
            else
                return "";
        }

        /// <summary>
        /// Ritorna l'utente corrente
        /// </summary>
        /// <returns></returns>
        public async Task<ApplicationUser> GetUser()
        {
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;

            if (userName == null)
                return null;

            var user = await _userManager.FindByNameAsync(userName);

            return user;

        }
        
        /// <summary>
        /// Verifica se l'utente loggato può accedere ai dati dell'aziende a lui non collegato
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CanAccessOtherCompany()
        {
            var user = await _userManager.FindByNameAsync(_httpContextAccessor.HttpContext?.User?.Identity?.Name);

            if (user != null && user.IdCompany != null && !user.IsDeleted)
            {
                if (await IsMainCompany(user.IdCompany.Value))
                {
                    return true;
                }
                else
                {
                    var company = await _context.Companies.FindAsync(user.IdCompany);
                    if (company != null && company.CompanyType == CompanyTypes.Reseller)
                    {
                        return true;
                    } 
                }
                return false;


                
            }
            else
                return false;   
        }

       
        

        public async Task<AuthorizationResult> CheckPolicy(ePolicy policy)
        {
            
            ClaimsPrincipal user = _httpContextAccessor.HttpContext?.User;
            
            if (user != null)
                return await _authorization.AuthorizeAsync(user, policy.ToString());
            else
            {
                return AuthorizationResult.Failed();
            }
        }

        public async Task<bool> CheckPolicy(string idUser, ePolicy policy)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(idUser);

            if (user == null || user.IsDeleted)
                return false;

            foreach (var role in PolicyRoles.vPoliyRoles[(int)policy])
            {
                if (await _userManager.IsInRoleAsync(user, role))
                    return true;
            }
            return false;

            
        }

        public async Task<ePolicy?> GetPolicy()
        {
            foreach (var policy in Enum.GetValues(typeof(ePolicy)))
            {
                var r = (await CheckPolicy((ePolicy)policy));

                if (r != null && r.Succeeded)
                    return (ePolicy)policy;
            }
            return null;
        }

        public async Task<List<ApplicationUser>> GetAdmins()
        {
            return (await _userManager.GetUsersInRoleAsync(eRoles.Admin.ToString())).ToList();
        }
        public async Task<bool> IsClient()
        {
            var policy = await GetPolicy();

            return (policy == null || policy == ePolicy.ClientRole);
        }

        public async Task<bool> IsAdmin(string? idUser = null)
        {
            if (idUser == null)
                return (await CheckPolicy(ePolicy.AdminRole)).Succeeded;
            else
                return (await CheckPolicy(idUser, ePolicy.AdminRole));
        }
        public async Task<bool> IsSuperUser()
        {
            return (await CheckPolicy(ePolicy.SuperUserRole)).Succeeded;
        }

        public async Task<bool> IsSuperUser(string idUser)
        {
            return (await CheckPolicy(idUser, ePolicy.SuperUserRole));
            
        }

        public async Task<bool> IsStandardUser()
        {

            return (await CheckPolicy(ePolicy.StandardRole)).Succeeded;

        }


        public async Task<bool> IsStandardUser(string? idUser)
        {
            if (idUser == null)
                return await IsStandardUser();
            else
                return (await CheckPolicy(idUser, ePolicy.StandardRole));
        }

        public async Task<bool> IsMainCompany(int idCompany)
        {
            return idCompany == await GetHeadQuarter();
        }

        public async Task<bool> BelongsToMainCompany()
        {
            var user = await GetUser();

            return await BelongsToMainCompany(user);
        }

       
        public async Task<bool> BelongsToMainCompany(ApplicationUser? user)
        {
           
            if (user == null)
                return await BelongsToMainCompany();
            else if (!user.IsDeleted)
                return user?.IdCompany == await GetHeadQuarter();
            else
                return false;
        }

        public async Task<bool> BelongsToMainCompany(string? idUser)
        {
            
            var user = await _userManager.FindByIdAsync(idUser);
            return await BelongsToMainCompany(user);
        }

        public async Task<List<string>> GetMainCompanyIdUsers()
        {
            int? idCompany = await GetHeadQuarter();

            return await GetCompanyIdUsers(idCompany);
        }

        public async Task<bool> BelongsToReseller()
        {
            var user = await GetUser();
            
            var company = await _context.Companies.FindAsync(user.IdCompany);

            return !user.IsDeleted && company.CompanyType == CompanyTypes.Reseller;
        }

        public async Task<List<int>> GetIdCompanies()
        {
            var user = await _userManager.FindByNameAsync(_httpContextAccessor.HttpContext?.User?.Identity?.Name);

            if (user != null && !user.IsDeleted && user.IdCompany != null)
            {

                return await GetIdCompanies((int)user.IdCompany);
            }
            return new List<int>();
        }

        public async Task<List<int>>GetIdCompanies(int idCompany)
        {
            var company = await _context.Companies.FindAsync(idCompany);

            if (company != null && company.CompanyType == CompanyTypes.Reseller)
            {
                var list = await _context.Companies.Where(x => x.IdReseller == idCompany || x.Id == idCompany).Select(x => x.Id).ToListAsync();

                return list;
            }
            return new List<int>();
        }

        /// <summary>
        /// Ritorna la compagnia dell'utente loggato
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public async Task<int?> GetIdCompany()
        {

            var item = await _userManager.FindByNameAsync(_httpContextAccessor.HttpContext.User.Identity.Name);

            var user = await _context.Users.Include(x => x.Company).Where(x => x.Id == item.Id).FirstOrDefaultAsync();

            if (user != null)
                return user.IdCompany;
            else
                return null;
        }

        public async Task<Company?> GetCompany()
        {

            var user = await GetUser();

            var company = await _context.Companies.FindAsync(user.IdCompany);

            _context.Entry(company).State = EntityState.Detached;

            return company;
        }


        public async Task<List<string>> GetCompanyIdUsers(int? idCompany)
        {
            return await _context.Users.Where(x => x.IdCompany == idCompany).Select(x=>x.Id).ToListAsync();
        }
        #region Tickets

        public async Task<int> TicketPermits(int idTicket, int IdCompany, string idUserAssigned)
        {
            int permits = await ObjectPermits(IdCompany, idUserAssigned);

            permits = PermitsHelper.ResEdit(permits);
            if (await CanEditTicket())
            {
                permits = PermitsHelper.SetEdit(permits);

            }
            if (await CanAssignTicket())
                permits = PermitsHelper.SetAssign(permits);

            if (await CanCloseTicket(idTicket))
                permits = PermitsHelper.SetClose(permits);

            if (await CanReOpenTicket(idTicket))
                permits = PermitsHelper.SetReOpen(permits);

            if (await CanViewInternalData())
                permits = PermitsHelper.SetInternalData(permits);
            return permits;
        }

        public async Task<bool> CanGetTicket(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
               
                return await CanGetObject(ticket.IdCompany);
                
            }
            return false;
        }
        /// <summary>
        /// Un utente puo' modificare un tcket
        /// 1) Se è almeno standard user e appartiene alla ditta principale
        /// 2)
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        public async Task<bool> CanEditTicket()
        {
            return await IsStandardUser() && await BelongsToMainCompany();

        }
        /// <summary>
        /// Indica se un utente può chiudere un ticket
        /// Un ticket puo' essere chiuso dll'utente a cui è stato assegnato e
        /// da un utente con ruoli di Admin o Super user
        /// </summary>
        /// <param name="IdTicket"></param>
        /// <returns></returns>
        public async Task<bool> CanCloseTicket(int IdTicket)
        {
            var user = await _userManager.FindByNameAsync(_httpContextAccessor.HttpContext.User.Identity.Name);

            var roles = await _userManager.GetRolesAsync(user);

            var ticket = await _context.Tickets.FindAsync(IdTicket);

            if (ticket != null)
            {
                return await IsSuperUser() || ticket.IdUserAssigned == user.Id;

            }
            else
                return false;
        }

        /// <summary>
        /// A ticket can reopen if all of the following conditions are true
        /// 1) The Ticket is closed
        /// 2) The User is at least a Standard User and belongs to Main Company
        /// </summary>
        /// <param name="idTicket"></param>
        /// <returns></returns>
        public async Task<bool> CanReOpenTicket(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket == null || !ticket.Closed)
                return false;

            

            return await IsStandardUser() && await BelongsToMainCompany();
        }
        public async Task<List<ApplicationUser>> GetUsersCanAssignTicket(int idTicket)
        {

            List<ApplicationUser> usersToAssign = new List<ApplicationUser>();
       

            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
                return await GetUsersCanAssignTicketType(ticket.IdType);
            }
            else
                return null;
        }
        public async Task<List<ApplicationUser>> GetUsersCanAssignTicketType(int idType)
        {
            List<ApplicationUser> usersToAssign = new List<ApplicationUser>();
            List<int> groups;
            List<string> users;

            groups = _context.Groups.Where(x => x.TicketTypes.Where(y => y.Id == idType).Any()).Select(x=>x.Id).ToList();
            users = _context.Users.Where(x => x.TicketTypes.Where(y => y.Id == idType).Any()).Select(x => x.Id).ToList();

            if (groups.Any() || users.Any())
            {
                if (groups.Any())
                {
                    var list = await _context.Users.Where(x => x.Groups.Where(y => groups.Contains(y.Id)).Any()).ToListAsync();
                    usersToAssign.AddRange(list);
                }

                if (users.Any())
                {
                    
                    var list = await _userManager.Users.Where(x => users.Contains(x.Id)).ToListAsync();
                    list = list.Where(x => !usersToAssign.Contains(x)).ToList();

                    usersToAssign.AddRange(list);
                }
            }
            else
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings != null)
                {
                    usersToAssign = await _userManager.Users.Where(x => x.IdCompany == settings.IdHeadQuarter).ToListAsync();
                }
            }
            
            return usersToAssign.ToList();
        }



        /// <summary>
        /// A ticket can assign to a user if any of the following condition occur:
        /// 1) If no user or group has been assigned to the ticket type: the ticket can be assigned to any user of the main comapany
        /// 2) If a list of users or groups has been assigned to the ticket type: the ticket can be assigned to a user in the list or a user belonging to one of groups.       
        /// </summary>
        /// <param name="idTicket"></param>
        /// <returns></returns>
        public async Task<bool> CanReceveTicket(int idTicket, string idUser)
        {
            bool ticketUsers, ticketGroups;

            var ticket = await _context.Tickets.Include(x => x.TicketType).ThenInclude(x => x.Users).Include(x => x.TicketType.Groups).Where(x => x.Id == idTicket).FirstOrDefaultAsync(); 

            if (ticket != null)
            {
                ticketUsers = ticket.TicketType.Users.Any();
                ticketGroups = ticket.TicketType.Groups.Any();

                if (ticketGroups || ticketUsers)
                {
                    if (ticketUsers)
                    {
                        return ticket.TicketType.Users.Where(x => x.Id == idUser).Any();
                    }

                    if (ticketGroups)
                    {
                        var groups = ticket.TicketType.Groups;
                        return _context.Groups.Where(x => groups.Contains(x) && x.Users.Where(x => x.Id == idUser).Any()).Any();

                    }
                }
                else
                {
                    var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                    if (settings != null)
                    {
                        return _userManager.Users.Where(x => x.IdCompany == settings.IdHeadQuarter).Where(x => x.Id == idUser).Any();
                    }
                    else
                        return true;
                }
            }
            return false;

        }

        

        /// <summary>
        /// The current user can assign a ticket if
        /// 1) He  belongs to the Main Company 
        /// 2) He at least a Standard User
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CanAssignTicket(string idUser = null)
        {
            
            return await IsStandardUser(idUser) && await BelongsToMainCompany(idUser);

        }

        

        public async Task<List<string>> UsersCanAssignTicket()
        {
            List<string> items = new List<string>();
            List<string> users = await GetMainCompanyIdUsers();

            foreach(var user in users)
            {
                if (await IsStandardUser(user))
                    items.Add(user);
            }
            return items;
            
        }
        public async Task<bool>CanViewInternalData()
        {
            return await BelongsToMainCompany();
        }
        #endregion

        #region TicketChat

        
        public async Task<bool> CanEditTicketChat(int id)
        {
            var ticketChat = await _context.TicketChats.FindAsync(id);

            if (ticketChat != null)
            {
                return await CanEditObject(ticketChat.IdUser);
            }
            else
            {
                
                return false;
            }
        }

        /// <summary>
        /// Un utente puoi inserire una Messaggio di chat del ticket se:
        /// 1) L'user è uno Standard user appartenete alla Main Company
        /// 2) L'user è l'utente che ha aperto il ticket
        /// 3) L'user e' l'utente a cui è stato assegnato il ticket
        /// 5) L'user è almeno uno StandardUser appartenente alla azienda appartente all'aienda del ticket
        /// </summary>
        /// <param name="idTicket"></param>
        /// <param name="idUser"></param>
        /// <returns></returns>
        public async Task<bool> CanInsertTicketChat(int idTicket)
        {
            Ticket ticket = await _context.Tickets.FindAsync(idTicket);
            string idUser = await IdUser();

            if (ticket != null)
            {
                return (await IsStandardUser() && await BelongsToMainCompany()) || ticket.IdCompany == await GetIdCompany() || ticket.IdUserOpened == idUser || ticket.IdUserAssigned == idUser;
            }
            else
                return false;

        }

        /// <summary>
        /// Un utente puo' leggere un messaggio se
        /// 1) Appartiene alla Main Company ed è un utente standard
        /// 2) Appartiene alla ditta del cliente che ha richiesto il ticket
        /// 3) E' l'utente a cui è stato assegnato il ticket
        /// 4) E' un utente a cui puo' essere assegnato il ticket. 
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        public async Task<bool> CanReadTicketChat(int idTicket, string? idUser = null)
        {
            Ticket ticket = await _context.Tickets.FindAsync(idTicket);
            if (idUser == null)
                idUser = await IdUser();

            if (ticket != null)
            {
                
                return (await IsStandardUser() && await BelongsToMainCompany()) || ticket.IdCompany == await GetIdCompany() || ticket.IdUserAssigned == idUser || await CanReceveTicket(idTicket, idUser);
            }
            else
                return false;
        }

        public async Task<string[]> GetUsersCanReadTicketChat(int idTicket)
        {
            List<string> list = new List<string>();

            var users = _userManager.Users;

            foreach (var user in users)
            {
                if (await CanReadTicketChat(idTicket, user.Id))
                    list.Add(user.UserName);
            }
           
            return list.ToArray();
        }

        /// <summary>
        /// L'alert di un nuovo messaggio ricevuto deve essere inviato a:
        /// 1) Se è il Cliente a inviare il messaggio: 
        ///     a) All'utente a cui è stato assegnato il ticket
        ///      
        /// 2) Se è un utente della main Company a inviare il messaggio
        ///     a) All'utente del cliente assegnato come referente nel tickt, se è stato impostato
        ///     b) Se non è stato impostato il referente del cliente per quel ticket, a tutti gli utenti del cliente 
        ///     c) Se l'utente che ha inviato il messaggio non è l'utente a cui è stato assegnato il ticket, il messaggio va inviato anche a ìll'utente assegnatario.
        /// </summary>
        /// <param name="idTicket"></param>
        /// <returns></returns>
        public async Task<List<string>?> GetUserSendTicketChatAlert(int idTicketChat)
        {
            int? idCompany;
            var ticketChat = await _context.TicketChats.Include(x=>x.User).Include(x => x.Ticket).FirstOrDefaultAsync(x => x.Id == idTicketChat);
            List<string>? list = null;
            

            if (ticketChat != null && ticketChat.User.IdCompany != null)     
            {
                idCompany = ticketChat.User.IdCompany;
                var ticket = ticketChat.Ticket;
                
                if (await IsMainCompany((int)idCompany))
                {
                    // Utente della main company

                    // Verifico se esiste il referente del cliente
                    if (ticket.IdUserCustomer != null)
                    {
                        list = new List<string>() { ticket.IdUserCustomer };
                    }
                    else
                    {
                        // Invio a tutti gli utenti del cliete.
                        list = await GetCompanyIdUsers(ticket.IdCompany);
                    }

                    if (ticketChat.IdUser != ticket.IdUserAssigned && ticket.IdUserAssigned != null)
                    {
                        list.Add(ticket.IdUserAssigned);
                    }
                }
                else if (idCompany == ticket.IdCompany)
                {
                    // Utente è cliente del Ticket

                    if (ticket.IdUserAssigned != null)
                    {
                        list = new List<string>() { ticket.IdUserAssigned };
                    }
                    else
                    {
                        list = await UsersCanAssignTicket();
                    }
                }
            }

            return list;

        }

        #endregion

        #region Talks

        /// <summary>
        /// Da implementare
        /// Un talk puo essere visualizzato se 
        /// Appartengo al gruppo dei commerciali ed è un mio cliente
        /// Oppure sono un super user
        /// </summary>
        /// <param name="idTalk"></param>
        /// <returns></returns>
        public async Task<bool> CanGetTalk(int idTalk)
        {
            var talk = await _context.Talks.FindAsync(idTalk);

            if (talk != null)
            {
                return await BelongsToMainCompany() && await IsStandardUser();
            }
            return false;
        }

        public async Task<bool> CanInsertTalk()
        {
            return await BelongsToMainCompany() && await IsStandardUser();
        }

        public async Task<bool> CanEditTalk(int idTalk)
        {
            var talk = await _context.Talks.FindAsync(idTalk);

            if (talk != null)
            {
                var resp =  await BelongsToMainCompany() && (await IsStandardUser() || await IdUser() == talk.IdUser);
                _context.Entry(talk).State = EntityState.Detached;
                return resp;
                
            }
            return false;
        }

        public async Task<bool> CanDeleteTalk(int idTalk)
        {
            var talk = await _context.Talks.FindAsync(idTalk);

            if (talk != null)
            {
                return await BelongsToMainCompany() &&
                    ((await IsStandardUser() && await IdUser() == talk.IdUser) || await IsAdmin());
            }
            return false;
        }


        public async Task<int> TalkPermits(int idTalk)
        {
            int permits = 0;

            if (await CanGetTalk(idTalk))
                permits =  PermitsHelper.SetRead(permits);

            if (await CanEditTalk(idTalk))
                permits = PermitsHelper.SetEdit(permits);

            if (await CanInsertObject())
                permits = PermitsHelper.SetInsert(permits);

            if (await CanDeleteTalk(idTalk))
                permits = PermitsHelper.SetDelete(permits);
            return permits;
        }
        #endregion

        #region Object Generic

        public async Task<int> ObjectPermits(int? objIdCompany, string objIdOwner)
        {
            int permits = 0;
            
            if (await CanReadObject(objIdCompany))
                permits = PermitsHelper.SetRead(permits);

            if (await CanInsertObject())
                permits = PermitsHelper.SetInsert(permits);

            if (await CanDeleteObject(objIdOwner))
            {
                permits = PermitsHelper.SetDelete(permits);
                permits = PermitsHelper.SetEdit(permits);
            }
            
           
            return permits;
        }

        
        public async Task<bool> CanGetObject(int? objIdCompany)
        {
            var resp = await CompanyCanAccess(objIdCompany);

            if (resp != null && resp.CanAccess)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// a User can read generic data if any of the following conditions occur:
        /// 1) He belongs to main company
        /// 2) He belongs to customer company
        /// 3) He's at least a Superuser
        /// </summary>
        /// <param name="idIntervention"></param>
        /// <returns></returns>
        public async Task<bool> CanReadObject(int? objIdCompany)
        {
          
            if (await IsSuperUser())
                return true; //User is at least a Superuser

            var user = await GetUser();


            if (user.IdCompany == await GetHeadQuarter())
            {
                // User belongs to the main company
                return true;
            }

            return objIdCompany == user.IdCompany;   // User belongs to the customer company

        }

        /// <summary>
        /// a User can insert a generic data if any of the following conditions occur:        
        /// 1) He's at least a Superuser
        /// </summary>
        /// <param name="objIdCompany"></param>
        /// <returns></returns>
        public async Task<bool> CanInsertObject()
        {

            return (await IsSuperUser());
            
        }

        /// <summary>
        /// 1) He's at least a Superuser
        /// 2) He 's the owner of the object
        /// /// </summary>
        /// <param name="objIdCompany"></param>
        /// <returns></returns>
        public async Task<bool> CanDeleteObject(string objIdOwner)
        {
            if (await IsSuperUser())
                return true;

            return (await IdUser() == objIdOwner);
        }

        public async Task<bool> CanEditObject(string objIdOwner)
        {
            if (await IsSuperUser())
                return true;

            return (await IdUser() == objIdOwner);
        }

        #endregion

        #region Articles

        /// <summary>
        /// Possono visualizzare gli articoli tutti gli utenti loggati
        /// Possono modificare o inserire articoli gli utenti con policy Standard
        /// Possono cancellare articoli gli utenti con policy Super User
        /// </summary>
        /// <returns></returns>
        public async Task<int> ArticlePermits()
        {
            int permits = 0;

            permits = PermitsHelper.SetRead(permits);

            if ((await CheckPolicy(ePolicy.StandardRole)).Succeeded)
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetInsert(permits);
            }

            if ((await CheckPolicy(ePolicy.SuperUserRole)).Succeeded)
            {
                permits = PermitsHelper.SetDelete(permits);

            }

            return permits;
        }

        public async Task<IQueryable<Article>> ArticlesQuery(IQueryable<Article>query, string? idUser = null)
        {
            if (idUser == null)
                idUser = await IdUser();

            var user = await _userManager.FindByIdAsync(idUser);

            var company = await _context.Companies.FindAsync(user.IdCompany);

            if (company == null)
            {
                query = query.Where(x => false);
            }
            else
            {
                if (! await IsMainCompany(company.Id))
                {
                    if (company.CompanyType == CompanyTypes.Customer)
                    {
                        query = query.Where(x => x.IdCompany == company.Id);
                    }
                    else if (company.CompanyType == CompanyTypes.Reseller)
                    {
                        var companiesReseller = await GetIdCompanies(company.Id);

                        query = query.Where(x => companiesReseller.Contains(x.Id));
                    }
                }
            }
            return query;
            
        }

        public async Task<bool> ArticleCanAccess(int? idArticle)
        {
            PermitResponse resp = new PermitResponse();

            var user = await GetUser();

            var article = await _context.Articles.FindAsync(idArticle);

            if (article == null || user == null || user.IdCompany == null)
                return false;


            if (await IsMainCompany((int)user.IdCompany))
            {
                return true;
            }
            else
            {
                var company = await _context.Companies.FindAsync(user.IdCompany);

                if (company != null && article.IdCompany != null)
                {
                    if (company.CompanyType == CompanyTypes.Customer)
                    {
                        return article.IdCompany == company.Id;

                    }
                    else if (company.CompanyType == CompanyTypes.Reseller)
                    {
                        var companiesReseller = await GetIdCompanies(company.Id);
                        return companiesReseller.Contains((int)article.IdCompany);
                    }
                }
            }
            return false;
        }

        #endregion


        #region Companies

        /// <summary>
        /// Possono visualizzare le ditte tutti gli utenti loggati
        /// Possono modificare o inserire ditte gli utenti con policy Standard
        /// Possono cancellare ditte gli utenti con policy Super User
        /// </summary>
        /// <returns></returns>
        public async Task<int> CompanyPermits()
        {
            int permits = 0;

            permits = PermitsHelper.SetRead(permits);

            if ((await CheckPolicy(ePolicy.StandardRole)).Succeeded)
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetInsert(permits);
            }

            if ((await CheckPolicy(ePolicy.SuperUserRole)).Succeeded)
            {
                permits = PermitsHelper.SetDelete(permits);

            }

            return permits;
        }

        /// <summary>
        /// verifica se puo' accedere alle informazioni di una ditta
        /// Se puo' accedere ritorna l'Id della ditta, altimenti ritorna l'id della ditta di appartenenza dell'utente.
        /// Se un utente non appartiene a nessuna ditta ritorna 0
       
        /// </summary>
        ///
        /// <returns></returns>
        public async Task<PermitResponse> CompanyCanAccess(int? idCompany)
        {
            PermitResponse resp = new PermitResponse();

            var user = await GetUser();

            if (user == null || user.IdCompany == null)
                return new PermitResponse() { CanAccess = false };

            if (await IsMainCompany((int)user.IdCompany))
            {
                return new PermitResponse() { CanAccess = true, IdCompany = idCompany };
            }
            else
            {
                var company = await _context.Companies.FindAsync(user.IdCompany);

                if (company != null)
                {
                    if (company.CompanyType == CompanyTypes.Customer)
                    {
                        if (idCompany != null)
                            return new PermitResponse() { CanAccess = idCompany == company.Id, IdCompany = user.IdCompany };
                        else
                            return new PermitResponse() { CanAccess = true, IdCompany = user.IdCompany };
                    }
                    else if (company.CompanyType == CompanyTypes.Reseller)
                    {


                        var companiesReseller = await GetIdCompanies(company.Id);

                        if (idCompany != null)
                        {
                            
                            
                            if (companiesReseller.Contains((int)idCompany))
                                return new PermitResponse() { CanAccess = true, IdCompany = idCompany };

                        }
                        else
                            return new PermitResponse() { CanAccess = true, IdCompany = null, IdCompanies = companiesReseller };
                    }
                }
                
                   

            }
            return new PermitResponse() { CanAccess = false, IdCompany = null };    
        }

        public async Task<CompanyTypes?> CompanyType()
        {

            var user = await GetUser();

            if (user == null || user.IdCompany == null)
                return null;

            var company = await _context.Companies.FindAsync(user.IdCompany);

            if (company != null)
            {
                if (await IsMainCompany(company.Id))
                    return CompanyTypes.HeadCompany;
                else
                    return company.CompanyType;
            }
            return null;
        }

        #endregion

        #region Products

        /// <summary>
        /// Possono visualizzare le ditte tutti gli utenti loggati
        /// Possono modificare o inserire ditte gli utenti con policy Standard
        /// Possono cancellare ditte gli utenti con policy Super User
        /// </summary>
        /// <returns></returns>
        public async Task<int> ProductPermits()
        {
            int permits = 0;

            permits = PermitsHelper.SetRead(permits);

            if ((await CheckPolicy(ePolicy.StandardRole)).Succeeded)
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetInsert(permits);
            }

            if ((await CheckPolicy(ePolicy.SuperUserRole)).Succeeded)
            {
                permits = PermitsHelper.SetDelete(permits);

            }

            return permits;
        }
        #endregion

        #region Users

        /// <summary>
        /// Possono visualizzare le ditte tutti gli utenti loggati
        /// Possono modificare o inserire ditte gli utenti con policy Standard
        /// Possono cancellare ditte gli utenti con policy Super User
        /// </summary>
        /// <returns></returns>
        public async Task<int> UserPermits()
        {
            int permits = 0;

            permits = PermitsHelper.SetRead(permits);

            if ((await CheckPolicy(ePolicy.StandardRole)).Succeeded)
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetInsert(permits);
            }

            if ((await CheckPolicy(ePolicy.SuperUserRole)).Succeeded)
            {
                permits = PermitsHelper.SetDelete(permits);

            }

            return permits;
        }

       
        #endregion

        #region Interventions

        /// <summary>
        /// 
        /// A user can read ticket's intervention if
        ///     1)
        /// </summary>
        /// <param name="idIntervention"></param>
        /// <returns></returns>
        public async Task<int> InterventionPermits(int idIntervention)
        {
            int permits = 0;
            var intervention = await _context.TicketsInterventions.FindAsync(idIntervention);

            string idUser = await IdUser();
            if (intervention != null)
            {
                // un utente puo leggere un intervento se 
                if (await CanReadIntervention(intervention))
                {
                    permits = PermitsHelper.SetRead(permits);

                    if ((await CheckPolicy(ePolicy.ClientRole)).Succeeded)
                    {
                        permits = PermitsHelper.SetInsert(permits);
                    }
                    if (await CanDeleteIntervention(intervention.IdUser))
                    {
                        permits = PermitsHelper.SetDelete(permits);
                        permits = PermitsHelper.SetEdit(permits);
                    }
                }
            }
            return permits;
        }
        /// <summary>
        /// a User can read intervention data if any of the following conditions occur:
        /// 1) He belongs to main company
        /// 2) He belongs to customer company
        /// 3) He's at least a Superuser
        /// </summary>
        /// <param name="idIntervention"></param>
        /// <returns></returns>
        public async Task<bool> CanReadIntervention(TicketIntervention intervention)
        {
            if (intervention == null)
                return false;

            if (await IsSuperUser()) 
                return true; //User is at least a Superuser

            var user = await GetUser();


            if (user.IdCompany == await GetHeadQuarter())
            {
                // User belongs to the main company
                return true;
            }

            return intervention.Ticket.IdCompany == user.IdCompany;   // User belongs to the customer company

        }

        /// <summary>
        /// a User can edit intervention data if any of the following conditions occur:
        /// 1) He belongs to main company
        /// 2) He's at least a Superuser
        /// </summary>
        /// <param name="idIntervention"></param>
        /// <returns></returns>
        public async Task<bool> CanEditIntervention(TicketIntervention intervention)
        {
            if (intervention == null)
                return false;

            if (await IsSuperUser())
                return true; //User is at least a Superuser

            var user = await GetUser();

            return (user.IdCompany == await GetHeadQuarter());
            
        }

        public async Task<bool> CanDeleteIntervention(string owner)
        {
            string idUser = await IdUser();
            AuthorizationResult result = await CheckPolicy(ePolicy.SuperUserRole);

            return (idUser == owner || result.Succeeded);
        }

        /// <summary>
        /// A user can download the intervention report if any of the following conditions occur:
        /// 1) He belongs to the main company
        /// 2) He belongs to the custoner company
        /// 3) He's at least a Superuser
        /// </summary>
        /// <param name="idTicket"></param>
        /// <returns></returns>
        public async Task<bool> CanDownloadInterventionReport(int idIntervention)
        {
            TicketIntervention ticketIntervention = await _context.TicketsInterventions.Include(x => x.Ticket).Where(x => x.Id == idIntervention).FirstOrDefaultAsync();

            if (ticketIntervention == null)
                return false;   // Intervention not found

            if ((await CheckPolicy(ePolicy.SuperUserRole)).Succeeded)
            {
                //User is at least a Superuser
                return true;
            }

            var user = await GetUser();


            if (user.IdCompany == await GetHeadQuarter())
            {
                // User belongs to the main company
                return true;
            }

            return ticketIntervention.Ticket.IdCompany == user.IdCompany;   // User belongs to the customer company

        }
        #endregion

        #region Attachment

        /// <summary>
        /// Possono visualizzare gli attachment gli utenti loggati e 
        /// Possono inserire attachment gli utenti loggati
        /// Possono cancellare un attachment gli utenti con policy Super User o l'utente che ha caricato l'attachment
        /// </summary>
        /// <returns></returns>
        public async Task<int> AttachmentPermits(int idAttachment)
        {
            int permits = 0;
            var attachment = await _context.Attachments.FindAsync(idAttachment);

            string idUser = await IdUser();
            if (attachment != null)
            {
            
                permits = PermitsHelper.SetRead(permits);

                if ((await CheckPolicy(ePolicy.ClientRole)).Succeeded)
                {                    
                    permits = PermitsHelper.SetInsert(permits);
                }
                if (await CanDeleteAttachment(attachment.IdUser))
                {
                    permits = PermitsHelper.SetDelete(permits);
                    permits = PermitsHelper.SetEdit(permits);
                }
            }
            return permits;
        }

        /// <summary>
        /// Un utente puo' eliminare un allegato se
        /// 1) L'allegato è stato caricato dall'utente
        /// 2) L'utente è almeno un SuperUser
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        /// 
        public async Task<bool> CanDeleteAttachment(string owner)
        {
            string idUser = await IdUser();
            AuthorizationResult result = await CheckPolicy(ePolicy.SuperUserRole);

            return (idUser == owner || result.Succeeded);
        }

        public async Task<bool> CanDeleteAttachment(int idAttachment)
        {
            var attachment = await _context.Attachments.FindAsync(idAttachment);

            return await CanDeleteAttachment(attachment.IdUser);
        }

        #endregion

        #region AccessoryType

        
        public bool CanGetAccessoryType()
        {
            return true;
        }

        public async Task<bool> CanInsertAccessoryType()
        {
            return await BelongsToMainCompany() && await IsStandardUser();
        }

        public async Task<bool> CanEditAccessoryType()
        {

            var resp = await BelongsToMainCompany() && await IsStandardUser();
            return resp;

        }

        public async Task<bool> CanDeleteAccessoryType()
        {
                return await BelongsToMainCompany() &&
                    await IsSuperUser();
        }


        public async Task<int> AccesoryTypePermits()
        {
            int permits = 0;

            if (CanGetAccessoryType())
                permits = PermitsHelper.SetRead(permits);

            if (await CanEditAccessoryType())
                permits = PermitsHelper.SetEdit(permits);

            if (await CanInsertAccessoryType())
                permits = PermitsHelper.SetInsert(permits);

            if (await CanDeleteAccessoryType())
                permits = PermitsHelper.SetDelete(permits);
            return permits;
        }
        #endregion

        #region Accessory


        public bool CanGetAccessory()
        {
            return true;
        }

        public async Task<bool> CanInsertAccessory()
        {
            return await BelongsToMainCompany() && await IsStandardUser();
        }

        public async Task<bool> CanEditAccessory()
        {

            var resp = await BelongsToMainCompany() && await IsStandardUser();
            return resp;

        }

        public async Task<bool> CanDeleteAccessory()
        {
            return await BelongsToMainCompany() &&
                await IsSuperUser();
        }


        public async Task<int> AccesoryPermits()
        {
            int permits = 0;

            if (CanGetAccessory())
                permits = PermitsHelper.SetRead(permits);

            if (await CanEditAccessory())
                permits = PermitsHelper.SetEdit(permits);

            if (await CanInsertAccessory())
                permits = PermitsHelper.SetInsert(permits);

            if (await CanDeleteAccessory())
                permits = PermitsHelper.SetDelete(permits);
            return permits;
        }
        #endregion

        #region ContractTypes

        public bool CanGetContractType()
        {
            return true;
        }

        public async Task<bool> CanInsertContractType()
        {
            return await BelongsToMainCompany() && await IsAdmin();
        }

        public async Task<bool> CanEditContractType()
        {

            var resp = await BelongsToMainCompany() && await IsAdmin();
            return resp;

        }

        public async Task<bool> CanDeleteContractType()
        {
            return await BelongsToMainCompany() &&
                await IsAdmin();
        }

        public async Task<int> ContractTypePermits()
        {
            int permits = 0;

            if (CanGetContractType())
                permits = PermitsHelper.SetRead(permits);

            if (await CanEditContractType())
                permits = PermitsHelper.SetEdit(permits);

            if (await CanInsertContractType())
                permits = PermitsHelper.SetInsert(permits);

            if (await CanDeleteContractType())
                permits = PermitsHelper.SetDelete(permits);
            return permits;
        }


        #endregion
        private async Task<int?> GetHeadQuarter()
        {
            var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

            return settings?.IdHeadQuarter;
        }
    }
}
