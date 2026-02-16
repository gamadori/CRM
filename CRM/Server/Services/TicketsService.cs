using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public class TicketsService: ITicketsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly UserManager<ApplicationUser> _userManager;
        public TicketsService(ApplicationDbContext context,  IHttpContextAccessor httpContextAccessor, 
            IPermitsService permitsService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _userManager = userManager;
        }

        public async Task<List<(string?, string?)>> GetEmails(int idTicket)
        {
            List<(string?, string?)> addresses = new List<(string?, string?)>();

            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
                var contact = await _context.Contacts.FindAsync(ticket.IdContact);

                if (contact != null)
                {
                    // Contatto assegnato al ticket
                    addresses.Add(new ( contact.Email, contact.Mobile ));
                }
                                
                var user = await _context.Users.Where(x => x.Id == ticket.IdUserOpened).FirstOrDefaultAsync();

                if (user != null)
                {
                    // Il ticket è stato aperto da un utente
                    addresses.Add(new(user.Email, user.PhoneNumber));
                }

                var listAdmin = await _permitsService.GetAdmins();

                foreach (var admin in listAdmin)
                {
                    if (!addresses.Where(x=>x.Item1 == admin.Email).Any())
                        addresses.Add((admin.Email, admin.PhoneNumber));
                }
                
                if (!addresses.Any())
                {
                    // Nessun contatto trovato vado recuperare gli utenti del cliente
                    var users = await _context.Users.Where(x => x.IdCompany == ticket.IdCompany).ToListAsync();

                    foreach(var item in users)
                    {
                        addresses.Add((item.Email, item.PhoneNumber));  
                    }
                }

                if (!addresses.Any())
                {
                    // Nessun contatto trovato recupero almeno la email della ditta
                }
            }

            return addresses;
        }

        public async Task<List<UserModel>> GetUsersCanAssignTicketAsync(int idTicket)
        {

            List<UserModel> usersToAssign = new List<UserModel>();


            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
                return await GetUsersCanAssignTicketTypeAsync(ticket.IdType);
            }
            else
                return new List<UserModel>();
        }
        public async Task<List<UserModel>> GetUsersCanAssignTicketTypeAsync(int idType)
        {
            List<UserModel> usersToAssign = new List<UserModel>();
            List<int> groups;
            List<string> users;
            List<int>? idCompanies = await _permitsService.GetIdCompanies();

            if (idCompanies == null || !idCompanies.Any())
                return new List<UserModel>();

            groups = _context.Groups.Where(x => x.TicketTypes.Where(y => y.Id == idType).Any()).Select(x => x.Id).ToList();
            users = _context.Users
                .Where(x => x.TicketTypes.Where(y => y.Id == idType
                    && x.IdCompany != null && idCompanies.Contains(x.IdCompany.Value)).Any())
                .Select(x => x.Id).ToList();

            if (groups.Any() || users.Any())
            {
                if (groups.Any())
                {
                    var list = await _context.Users.Where(x => x.Groups.Where(y => groups.Contains(y.Id)).Any()).ToListAsync();
                    usersToAssign.AddRange(list.Select(x=>x.ToUserModel()));
                }

                if (users.Any())
                {

                    var list = await _userManager.Users.Where(x => users.Contains(x.Id)).Select(x=>x.ToUserModel()).ToListAsync();
                    list = list.Where(x => !usersToAssign.Contains(x)).ToList();

                    usersToAssign.AddRange(list);
                }
            }
            else
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings != null && await _permitsService.BelongsToMainCompany())
                {
                    usersToAssign = await _userManager.Users.Where(x => x.IdCompany != null && idCompanies.Contains( x.IdCompany.Value)).Select(x=>x.ToUserModel()).ToListAsync();
                }
            }

            return usersToAssign.ToList();
        }
    }
}
