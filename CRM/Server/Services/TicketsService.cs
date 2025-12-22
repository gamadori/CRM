using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public class TicketsService: ITicketsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        public TicketsService(ApplicationDbContext context,  IHttpContextAccessor httpContextAccessor, IPermitsService permitsService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
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
    }
}
