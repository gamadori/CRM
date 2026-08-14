using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Extensions;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class TicketsService : ITicketsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        private readonly ICommessaFasiService _commessaFasiService;
        private readonly ITicketBlockNotificationService _ticketBlockNotifications;
        
        public TicketsService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService, UserManager<ApplicationUser> userManager,
            ILogEventService logEventService, ILanguagesService languagesService,
            ICommessaFasiService projectTasksService,
            ITicketBlockNotificationService ticketBlockNotifications)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _userManager = userManager;
            _logEventService = logEventService;
            _languagesService = languagesService;
            _commessaFasiService = projectTasksService;
            _ticketBlockNotifications = ticketBlockNotifications;
        }

        #region Existing methods

        public async Task<List<(string?, string?)>> GetEmails(int idTicket)
        {
            List<(string?, string?)> addresses = new List<(string?, string?)>();

            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
                var contact = await _context.Contacts.FindAsync(ticket.IdContact);

                if (contact != null)
                {
                    addresses.Add(new(contact.Email, contact.Mobile));
                }

                var user = await _context.Users.Where(x => x.Id == ticket.IdUserOpened).FirstOrDefaultAsync();

                if (user != null)
                {
                    addresses.Add(new(user.Email, user.PhoneNumber));
                }

                var listAdmin = await _permitsService.GetAdmins();

                foreach (var admin in listAdmin)
                {
                    if (!addresses.Where(x => x.Item1 == admin.Email).Any())
                        addresses.Add((admin.Email, admin.PhoneNumber));
                }

                if (!addresses.Any())
                {
                    var users = await _context.Users.Where(x => x.IdCompany == ticket.IdCompany).ToListAsync();

                    foreach (var item in users)
                    {
                        addresses.Add((item.Email, item.PhoneNumber));
                    }
                }
            }

            return addresses;
        }

        public async Task<List<UserModel>> GetUsersCanAssignTicketAsync(int idTicket)
        {
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
                    usersToAssign.AddRange(list.Select(x => x.ToUserModel()));
                }

                if (users.Any())
                {
                    var list = await _userManager.Users.Where(x => users.Contains(x.Id)).Select(x => x.ToUserModel()).ToListAsync();
                    list = list.Where(x => !usersToAssign.Contains(x)).ToList();
                    usersToAssign.AddRange(list);
                }
            }
            else
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings != null && await _permitsService.BelongsToHeadCompany())
                {
                    usersToAssign = await _userManager.Users.Where(x => x.IdCompany != null && idCompanies.Contains(x.IdCompany.Value)).Select(x => x.ToUserModel()).ToListAsync();
                }
            }

            return usersToAssign.ToList();
        }

        #endregion

        #region CRUD

        public async Task<Ticket?> GetItemAsync(int id)
        {
            try
            {
                var query = _context.Tickets.Include(x => x.Company).Include(x => x.TicketType)
                    .Include(x => x.Article).ThenInclude(x => x.Product).Where(x => x.Id == id);

                var ticket = await (await ApplyVisibilityScopeAsync(query)).FirstOrDefaultAsync();

                if (ticket != null)
                {
                    await SetTicketStateAsync(ticket);
                }

                return ticket;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetItemAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<TicketDTO?> GetDetailsAsync(int id)
        {
            try
            {
                var idLanguage = await _languagesService.GetIdLanguage();
                

                var tickets = _context.Tickets.Where(x => x.Id == id).Include(x => x.UserOpened).AsQueryable();
                tickets = await ApplyVisibilityScopeAsync(tickets);

                var ticketModel = await tickets.Select(x => new TicketDTO()
                {
                    Id = x.Id,
                    Date = x.Date,
                    DateEnd = x.DateEnd,
                    // La scadenza mancava anche qui: la scheda e l'anteprima non potevano dire
                    // entro quando il ticket va chiuso.
                    DateExpired = x.DateExpired,
                    DateOpened = x.DateOpened,
                    DateClosed = x.DateClosed,
                    Time = x.Time,
                    Company = x.Company.RagioneSociale,
                    Product = (x.Product != null) ? x.Product.Name : "",
                    Article = (x.Article != null) ? x.Article.SerialNumber : "",
                    IdDeal = x.IdDeal,
                    DealName = x.Deal != null ? x.Deal.Name : "",
                    IdCommessaFase = x.IdCommessaFase,
                    CommessaFaseName = x.CommessaFase != null ? x.CommessaFase.Name : "",
                    IdCommessa = x.CommessaFase != null ? x.CommessaFase.IdCommessa : (int?)null,
                    CommessaCode = x.CommessaFase != null && x.CommessaFase.Commessa != null ? (x.CommessaFase.Commessa.Code ?? "") : "",
                    IdUserAssigned = x.IdUserAssigned,
                    IdGroupAssigned = x.IdGroupAssigned,
                    GroupAssigned = x.GroupAssigned != null ? x.GroupAssigned.Name : "",
                    AiSuggestedGroupId = x.AiSuggestedGroupId,
                    AiSuggestedGroup = x.AiSuggestedGroup != null ? x.AiSuggestedGroup.Name : null,
                    AiRoutingConfidence = x.AiRoutingConfidence,
                    AiRoutingReason = x.AiRoutingReason,
                    AiRoutedAt = x.AiRoutedAt,
                    AiRoutingApplied = x.AiRoutingApplied,
                    AiRoutingOutcome = x.AiRoutingOutcome,
                    IdCompany = x.IdCompany,
                    IdState = x.IdState,
                    IdUserOpened = x.IdUserOpened,
                    UserOpened = (x.UserOpened != null) ? x.UserOpened.NameComplete : "",
                    UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                    UserClosed = (x.UserClosed != null) ? x.UserClosed.NameComplete : "",
                    Description = x.Description,
                    OperationalSummary = x.OperationalSummary,
                    OperationalSummaryUpdatedAt = x.OperationalSummaryUpdatedAt,
                    OperationalSummaryUpdatedBy = x.OperationalSummaryUpdatedBy,
                    OperationalSummaryUpdatedByName = x.OperationalSummaryUpdatedByUser != null ? x.OperationalSummaryUpdatedByUser.NameComplete : "",
                    IsBlocked = x.IsBlocked,
                    BlockReason = x.BlockReason,
                    BlockedAt = x.BlockedAt,
                    IdBlockedBy = x.IdBlockedBy,
                    BlockedByName = x.BlockedByUser != null ? x.BlockedByUser.NameComplete : "",
                    BlockResolvedAt = x.BlockResolvedAt,
                    IdBlockResolvedBy = x.IdBlockResolvedBy,
                    BlockResolvedByName = x.BlockResolvedByUser != null ? x.BlockResolvedByUser.NameComplete : "",
                    BlockResolutionNote = x.BlockResolutionNote,
                    IdType = x.IdType,
                    // Traduzione se c'e', altrimenti la descrizione base del tipo: prima il ripiego
                    // era la stringa vuota, e con TicketTypesLanguages non popolata (o l'utente
                    // senza LanguageCode) il tipo ticket spariva del tutto dalla scheda.
                    DescType = x.TicketType.Languages
                        .Where(l => l.IdLanguage == idLanguage)
                        .Select(l => l.Name)
                        .FirstOrDefault() ?? x.TicketType.Desc,
                    TicketType = x.TicketType,
                    ContactName = x.Contact != null ? x.Contact.NameComplete : "",
                    CloseDescription = x.CloseDescription,
                    Closed = x.Closed,
                }).FirstOrDefaultAsync();

                if (ticketModel != null)
                {
                    // Ore fatturabili e ripartizione per tipo: stessa fonte della lista, cosi' i
                    // due numeri coincidono sempre.
                    TicketBillableMinutes.ApplyTo(ticketModel, await TicketBillableMinutes.ForTicketAsync(_context, id));
                    await SetTicketStateAsync(ticketModel);
                }

                return ticketModel;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetDetailsAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<ObjectView<TicketDTO, string>> GetPagingAsync(TicketFilter args)
        {
            try
            {
                string idUser = await _permitsService.IdUser();
                var isClient = await _permitsService.IsClient();
                DateTime dateTo;

                IQueryable<Ticket> tickets = _context.Tickets;

                if (args.OrderBy != null)
                {
                    tickets = tickets.OrderBy(args.OrderBy);
                }
                else
                {
                    tickets = tickets.OrderByDescending(x => x.Date);
                }

                // Sostituisce il vecchio filtro su CanAccessOtherCompany: quello non scattava per i
                // rivenditori, che si ritrovavano a vedere i ticket di tutte le aziende.
                tickets = await ApplyVisibilityScopeAsync(tickets);

                if (args.DateFrom != null)
                {
                    tickets = tickets.Where(x => x.Date >= args.DateFrom || x.DateEnd >= args.DateFrom);
                }

                if (args.DateTo != null)
                {
                    dateTo = args.DateTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.Date < dateTo || x.DateEnd < dateTo);
                }

                if (args.DateClosedFrom != null)
                {
                    tickets = tickets.Where(x => x.DateClosed >= args.DateClosedFrom);
                }

                if (args.DateClosedTo != null)
                {
                    dateTo = args.DateClosedTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.DateClosed < dateTo);
                }

                if (args.DateExpiredFrom != null)
                {
                    tickets = tickets.Where(x => x.DateExpired >= args.DateExpiredFrom);
                }

                if (args.DateClosedTo != null)
                {
                    dateTo = args.DateClosedTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.DateClosed < dateTo);
                }

                if (args.IdCompany != null)
                {
                    tickets = tickets.Where(x => x.IdCompany == args.IdCompany);
                }

                if (args.IdArticle != null)
                    tickets = tickets.Where(x => x.IdArticle == args.IdArticle);

                if (args.IdUserOpened != null)
                {
                    tickets = tickets.Where(x => x.IdUserOpened == args.IdUserOpened);
                }

                // NotAssigned e ToClaim selezionano per definizione ticket senza assegnatario:
                // applicarci sopra il filtro per utente li azzererebbe. NewMessage e' gia'
                // ristretto ai messaggi non letti dall'utente corrente.
                if (args.IdUserAssigned != null
                    && args.TypeSearch != (int)TicketTypeSearch.NotAssigned
                    && args.TypeSearch != (int)TicketTypeSearch.ToClaim
                    && args.TypeSearch != (int)TicketTypeSearch.NewMessage)
                {
                    if (args.ViewNotAssigned)
                        tickets = tickets.Where(x => (x.IdUserAssigned == args.IdUserAssigned || x.IdUserAssigned == null));
                    else
                        tickets = tickets.Where(x => x.IdUserAssigned == args.IdUserAssigned || x.AssignedUsers.Where(y => y.IdUser == args.IdUserAssigned).Any());
                }


                if (args.IdDeal != null)
                {
                    tickets = tickets.Where(x => x.IdDeal == args.IdDeal);
                }

                if (args.IdCommessaFase != null)
                {
                    tickets = tickets.Where(x => x.IdCommessaFase == args.IdCommessaFase);
                }

                if (args.IdCommessa != null)
                {
                    tickets = tickets.Where(x => x.CommessaFase != null && x.CommessaFase.IdCommessa == args.IdCommessa);
                }

                // Natura del lavoro: separa la produzione dall'assistenza. Esisteva solo
                // sull'endpoint della pianificazione; nell'elenco il parametro arrivava e veniva
                // ignorato in silenzio. Null lascia passare tutto.
                if (args.HasCommessa == true)
                {
                    tickets = tickets.Where(x => x.IdCommessaFase != null);
                }
                else if (args.HasCommessa == false)
                {
                    tickets = tickets.Where(x => x.IdCommessaFase == null);
                }

                if (args.IsBlocked != null)
                {
                    tickets = tickets.Where(x => x.IsBlocked == args.IsBlocked);
                }

                tickets = FilterByType(tickets, (TicketTypeSearch)args.TypeSearch, idUser, isClient);

                if (args.Filter != null && args.Filter.Length > 0)
                {
                    tickets = tickets.Where(args.Filter);
                }

                var totalWork = await TicketBillableMinutes.TotalAsync(_context, tickets);

                int count = await tickets.CountAsync();

                if (tickets != null && args?.Skip != null && args.Top != null)
                {
                    tickets = tickets.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var ticketModel = tickets.Select(x => new TicketDTO()
                {
                    Id = x.Id,
                    Date = x.Date,
                    DateOpened = x.DateOpened,
                    DateEnd = x.DateEnd,
                    // La scadenza mancava nella proiezione dell'elenco: e' l'informazione che dice
                    // cosa fare per primo, e senza di essa la colonna resta vuota.
                    DateExpired = x.DateExpired,
                    DateClosed = x.DateClosed,
                    Company = x.Company!.RagioneSociale,
                    Product = (x.Product != null) ? x.Product.Name : "",
                    Article = (x.Article != null) ? x.Article.SerialNumber : "",
                    IdDeal = x.IdDeal,
                    DealName = x.Deal != null ? x.Deal.Name : "",
                    IdCommessaFase = x.IdCommessaFase,
                    CommessaFaseName = x.CommessaFase != null ? x.CommessaFase.Name : "",
                    IdCommessa = x.CommessaFase != null ? x.CommessaFase.IdCommessa : (int?)null,
                    CommessaCode = x.CommessaFase != null && x.CommessaFase.Commessa != null ? (x.CommessaFase.Commessa.Code ?? "") : "",
                    IdUserAssigned = x.IdUserAssigned,
                    IdGroupAssigned = x.IdGroupAssigned,
                    GroupAssigned = x.GroupAssigned != null ? x.GroupAssigned.Name : "",
                    AiSuggestedGroupId = x.AiSuggestedGroupId,
                    AiSuggestedGroup = x.AiSuggestedGroup != null ? x.AiSuggestedGroup.Name : null,
                    AiRoutingConfidence = x.AiRoutingConfidence,
                    AiRoutingApplied = x.AiRoutingApplied,
                    AiRoutingOutcome = x.AiRoutingOutcome,
                    IdCompany = x.IdCompany,
                    IdState = x.IdState,
                    IdUserOpened = x.IdUserOpened,
                    UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                    Invoiced = x.Invoiced,
                    Description = x.Description,
                    OperationalSummary = x.OperationalSummary,
                    OperationalSummaryUpdatedAt = x.OperationalSummaryUpdatedAt,
                    OperationalSummaryUpdatedBy = x.OperationalSummaryUpdatedBy,
                    OperationalSummaryUpdatedByName = x.OperationalSummaryUpdatedByUser != null ? x.OperationalSummaryUpdatedByUser.NameComplete : "",
                    IsBlocked = x.IsBlocked,
                    BlockReason = x.BlockReason,
                    BlockedAt = x.BlockedAt,
                    IdBlockedBy = x.IdBlockedBy,
                    BlockedByName = x.BlockedByUser != null ? x.BlockedByUser.NameComplete : "",
                    BlockResolvedAt = x.BlockResolvedAt,
                    IdBlockResolvedBy = x.IdBlockResolvedBy,
                    BlockResolvedByName = x.BlockResolvedByUser != null ? x.BlockResolvedByUser.NameComplete : "",
                    BlockResolutionNote = x.BlockResolutionNote,
                    ContactName = x.Contact != null ? x.Contact.NameComplete : "",
                    Time = x.Time,
                    Closed = x.Closed,
                });

                var items = await ticketModel.ToListAsync();

                // Una query sola per tutta la pagina, non una per riga.
                var minutiPerTicket = await TicketBillableMinutes.ByTicketAsync(_context, items.Select(t => t.Id).ToList());

                foreach (var t in items)
                {
                    TicketBillableMinutes.ApplyTo(t, minutiPerTicket.TryGetValue(t.Id, out var b)
                        ? b
                        : new TicketBillableMinutes.Breakdown());
                }

                // Stato, permessi e pulsanti per tutta la pagina in blocco: vedi ApplyListStateAsync.
                await ApplyListStateAsync(items);

                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                };

                ObjectView<TicketDTO, string> ticketView = new ObjectView<TicketDTO, string>();
                ticketView.Total = DateTimeHelper.MinuteFormat(totalWork);
                ticketView.Items = items;

                return ticketView;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetPagingAsync), EventsTypes.Error, ex);
                return new ObjectView<TicketDTO, string>();
            }
        }

        public async Task<Ticket> PostAsync(Ticket ticket)
        {

            try
            {
                string idUserOpened = await _permitsService.IdUser();
                int day = await GetDayBeforeExpired(ticket.Id);

                ticket.DateOpened = DateTime.Now;

                if (ticket.Date == null)
                {
                    ticket.Date = ticket.DateOpened;
                }

                ticket.IdUserOpened = idUserOpened;
                ticket.DateExpired = ticket.Date?.AddWorkdays(day);
                await SetRequesterFromOpenerAsync(ticket, idUserOpened);
                await NormalizeCommessaFaseLinkAsync(ticket);

                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(ticket.IdCommessaFase);

                return ticket;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(PostAsync), EventsTypes.Error, ex);
                throw;
            }
        }

        public async Task<bool> PutAsync(int id, Ticket ticket)
        {
            try
            {
                var current = await _context.Tickets
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new
                    {
                        x.OperationalSummary,
                        x.OperationalSummaryUpdatedAt,
                        x.OperationalSummaryUpdatedBy,
                        x.AiSuggestedGroupId,
                        x.AiRoutingConfidence,
                        x.AiRoutingReason,
                        x.AiRoutedAt,
                        x.AiRoutingApplied,
                        x.AiRoutingOutcome
                    })
                    .FirstOrDefaultAsync();

                if (current == null)
                    return false;

                var previousTaskId = await _context.Tickets
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => x.IdCommessaFase)
                    .FirstOrDefaultAsync();

                ticket.OperationalSummary = current.OperationalSummary;
                ticket.OperationalSummaryUpdatedAt = current.OperationalSummaryUpdatedAt;
                ticket.OperationalSummaryUpdatedBy = current.OperationalSummaryUpdatedBy;

                // Lo storico dello smistamento AI e' di competenza del server: il client non lo
                // rimanda indietro e non deve poterlo sovrascrivere. Cambia solo l'esito, che qui
                // registra se il gruppo scelto dall'AI e' stato confermato o corretto a mano.
                ticket.AiSuggestedGroupId = current.AiSuggestedGroupId;
                ticket.AiRoutingConfidence = current.AiRoutingConfidence;
                ticket.AiRoutingReason = current.AiRoutingReason;
                ticket.AiRoutedAt = current.AiRoutedAt;
                ticket.AiRoutingApplied = current.AiRoutingApplied;
                ticket.AiRoutingOutcome = TicketRouting.TicketRoutingOutcomes.AfterGroupChange(
                    current.AiRoutingOutcome, current.AiSuggestedGroupId, ticket.IdGroupAssigned);

                await NormalizeCommessaFaseLinkAsync(ticket);

                ticket.IdCompanyAssigned =
                    (ticket.IdUserAssigned != null) ? _context.Users.Where(x => x.Id == ticket.IdUserAssigned).Select(x => x.IdCompany).FirstOrDefault() : null;

                _context.Entry(ticket).State = EntityState.Modified;

                if (!await _permitsService.IsAdmin())
                    _context.Entry(ticket).Property(x => x.Invoiced).IsModified = false;

                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(previousTaskId);
                await _commessaFasiService.RecomputeFaseProgressAsync(ticket.IdCommessaFase);
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(PutAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<DeleteTicketResult> DeleteAsync(int id)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null)
                    return DeleteTicketResult.Fail(DeleteTicketError.TicketNotFound, $"Ticket con ID {id} non trovato");

                await DetachExpenseReceiptsAsync(id);

                var taskId = ticket.IdCommessaFase;
                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(taskId);
                return DeleteTicketResult.Ok();
            }
            catch (Exception ex)
            {
                // Il contesto ha ancora la cancellazione fallita fra le modifiche in sospeso:
                // qualunque SaveChangesAsync successivo - compreso quello con cui il log si scrive,
                // che usa lo stesso contesto - riprova la stessa DELETE e rilancia la stessa
                // eccezione. Senza scartarle, l'errore non finisce nel log e non arriva a chi
                // guarda: il ticket semplicemente non si cancellava, in silenzio.
                _context.ChangeTracker.Clear();

                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(DeleteAsync), EventsTypes.Error, ex);

                return DeleteTicketResult.Fail(DeleteTicketError.Unexpected,
                    "Eliminazione non riuscita: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        /// <summary>
        /// Sgancia dalle note spese gli interventi del ticket, prima che la cancellazione li porti via.
        /// <para>
        /// Una nota spese sopravvive al lavoro a cui era collegata: sono soldi che qualcuno ha
        /// anticipato e che gli vanno rimborsati comunque, quindi la spesa diventa un costo
        /// generale invece di sparire insieme al ticket. E' la stessa regola su cui e' costruito il
        /// modulo - persona e data sono la spina dorsale, il contesto e' facoltativo.
        /// </para>
        /// <para>
        /// Serve farlo qui: il vincolo verso <c>TicketsInterventions</c> e' NO ACTION (SET NULL a
        /// livello di database creerebbe percorsi di cascata multipli), quindi senza questo passo
        /// SQL Server rifiuta la cancellazione di qualunque ticket con spese registrate.
        /// </para>
        /// </summary>
        private async Task DetachExpenseReceiptsAsync(int idTicket)
        {
            var receipts = await _context.ExpenseReceipts
                .Where(r => r.TicketInterventionId != null
                            && r.TicketIntervention!.IdTicket == idTicket)
                .ToListAsync();

            if (receipts.Count == 0)
                return;

            foreach (var receipt in receipts)
                receipt.TicketInterventionId = null;

            await _context.SaveChangesAsync();
        }

        #endregion

        #region Close / ReOpen

        public async Task<CloseTicketResult> CloseAsync(int id, TicketClose model)
        {
            try
            {
                var ticketState = await GetIdState(eTicketStates.Closed);

                Ticket? ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null)
                    return CloseTicketResult.Fail(CloseTicketError.TicketNotFound, $"Ticket con ID {id} non trovato");

                if (ticket.IsBlocked)
                    return CloseTicketResult.Fail(CloseTicketError.Blocked,
                        "Il ticket e' bloccato. Risolvi il blocco prima di chiuderlo.");

                // Il vincolo sta qui e non solo nella UI: questa e' l'unica strada verso la chiusura
                // (nessun altro punto scrive Closed = true), quindi e' anche l'unico posto che serve
                // presidiare perche' non sia aggirabile chiamando l'API a mano.
                var requiresIntervention = await RequiresInterventionToCloseAsync(ticket.IdType);

                if (requiresIntervention && !await HasInterventionAsync(id))
                    return CloseTicketResult.Fail(CloseTicketError.InterventionRequired,
                        "Questo tipo di ticket richiede almeno un intervento registrato per essere chiuso: "
                        + "registra l'intervento con ore e attivita' svolte, poi chiudi.");

                ticket.DateClosed = DateTime.Now;
                ticket.CloseDescription = model.Description;
                ticket.CloseNote = model.Note;
                ticket.IdUserClosed = await _permitsService.IdUser();

                // Sui tipi che pretendono l'intervento la modalita' e' sull'intervento: il form di
                // chiusura non la chiede piu' e sovrascriverla con il default dell'enum (0 = Telefono)
                // falserebbe lo storico dei ticket chiusi prima di questo vincolo.
                if (!requiresIntervention)
                    ticket.Support = model.Support;

                ticket.Closed = true;
                ticket.IdState = ticketState?.Id;

                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(ticket.IdCommessaFase);
                return CloseTicketResult.Ok();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(CloseAsync), EventsTypes.Error, ex);
                return CloseTicketResult.Fail(CloseTicketError.Unexpected, "Errore nella chiusura del ticket");
            }
        }

        public async Task<TicketClosePreconditionDTO?> GetClosePreconditionAsync(int idTicket)
        {
            try
            {
                var ticket = await _context.Tickets
                    .AsNoTracking()
                    .Where(t => t.Id == idTicket)
                    .Select(t => new { t.Id, t.IdType, t.Closed, t.IsBlocked })
                    .FirstOrDefaultAsync();

                if (ticket == null)
                    return null;

                var precondition = new TicketClosePreconditionDTO
                {
                    IdTicket = ticket.Id,
                    Closed = ticket.Closed,
                    IsBlocked = ticket.IsBlocked,
                    RequiresIntervention = await RequiresInterventionToCloseAsync(ticket.IdType),
                    InterventionCount = await _context.TicketsInterventions
                        .AsNoTracking()
                        .CountAsync(i => i.IdTicket == idTicket)
                };

                // Stesso ordine di verifica di CloseAsync: l'operatore legge il primo ostacolo reale.
                if (ticket.IsBlocked)
                    precondition.BlockReason = "Il ticket e' bloccato: risolvi il blocco prima di chiuderlo.";
                else if (precondition.RequiresIntervention && precondition.InterventionCount == 0)
                    precondition.BlockReason = "Questo tipo di ticket richiede almeno un intervento registrato per essere chiuso.";

                precondition.CanClose = precondition.BlockReason == null;

                return precondition;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetClosePreconditionAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        /// <summary>
        /// Un tipo mancante non deve bloccare la chiusura: senza tipo non c'e' una regola da
        /// applicare, e fermare il ticket per un dato di configurazione assente sarebbe arbitrario.
        /// </summary>
        private async Task<bool> RequiresInterventionToCloseAsync(int idType)
            => await _context.TicketTypes
                .AsNoTracking()
                .Where(x => x.Id == idType)
                .Select(x => (bool?)x.RequiresIntervention)
                .FirstOrDefaultAsync() ?? false;

        private async Task<bool> HasInterventionAsync(int idTicket)
            => await _context.TicketsInterventions
                .AsNoTracking()
                .AnyAsync(i => i.IdTicket == idTicket);

        public async Task<bool> ReOpenAsync(int id)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null) return false;

                ticket.Closed = false;
                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(ticket.IdCommessaFase);
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(ReOpenAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> BlockAsync(int id, TicketBlockRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                    return FailTicket("Indica il motivo del blocco.", System.Net.HttpStatusCode.BadRequest);

                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null)
                    return FailTicket("Ticket non trovato.", System.Net.HttpStatusCode.NotFound);

                if (ticket.Closed)
                    return FailTicket("Un ticket chiuso non puo' essere bloccato.", System.Net.HttpStatusCode.Conflict);

                var currentUserId = await _permitsService.IdUser();
                ticket.IsBlocked = true;
                ticket.BlockReason = request.Reason.Trim();
                ticket.BlockedAt = DateTime.Now;
                ticket.IdBlockedBy = currentUserId;
                ticket.BlockResolvedAt = null;
                ticket.IdBlockResolvedBy = null;
                ticket.BlockResolutionNote = null;

                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(ticket.IdCommessaFase);
                await _ticketBlockNotifications.NotifyBlockedAsync(ticket.Id);

                return await ReloadTicketResponseAsync(ticket.Id, "Blocco segnalato.");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(BlockAsync), EventsTypes.Error, ex);
                return FailTicket("Errore durante il blocco del ticket.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> UnblockAsync(int id, TicketUnblockRequest request)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null)
                    return FailTicket("Ticket non trovato.", System.Net.HttpStatusCode.NotFound);

                if (!ticket.IsBlocked)
                    return await ReloadTicketResponseAsync(ticket.Id, "Il ticket non risulta bloccato.");

                var currentUserId = await _permitsService.IdUser();
                ticket.IsBlocked = false;
                ticket.BlockResolvedAt = DateTime.Now;
                ticket.IdBlockResolvedBy = currentUserId;
                ticket.BlockResolutionNote = string.IsNullOrWhiteSpace(request?.ResolutionNote)
                    ? null
                    : request.ResolutionNote.Trim();

                await _context.SaveChangesAsync();
                await _commessaFasiService.RecomputeFaseProgressAsync(ticket.IdCommessaFase);
                await _ticketBlockNotifications.NotifyUnblockedAsync(ticket.Id);

                return await ReloadTicketResponseAsync(ticket.Id, "Ticket sbloccato.");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(UnblockAsync), EventsTypes.Error, ex);
                return FailTicket("Errore durante lo sblocco del ticket.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        #endregion

        #region State

        /// <summary>
        /// Un ticket solo: stessa strada dell'elenco, con una pagina da un elemento. Tenere due
        /// implementazioni delle stesse regole significherebbe vederle divergere, e la scheda
        /// direbbe una cosa diversa dalla lista sullo stesso ticket.
        /// </summary>
        public Task SetTicketStateAsync(TicketDTO ticket)
            => ApplyListStateAsync(new List<TicketDTO> { ticket });

        /// <summary>
        /// Stato, permessi e pulsanti di una pagina di elenco, calcolati in blocco.
        /// <para>
        /// Prima ogni riga passava da SetTicketStateAsync, che per ogni ticket rileggeva il ticket
        /// stesso, la tabella degli stati, il tipo, le impostazioni, le assegnazioni e l'intera
        /// catena dei permessi: una pagina da dieci righe faceva centinaia di viaggi al database, ed
        /// e' il motivo per cui l'elenco era lento. Qui i fatti che dipendono dal singolo ticket si
        /// leggono tutti insieme (una query), quelli che dipendono dall'utente una volta sola, e la
        /// decisione per riga diventa un calcolo in memoria.
        /// </para>
        /// <para>
        /// Le regole non cambiano: sono le stesse di GetTicketIdState, CanCurrentUserClaimTicketAsync
        /// e CanCurrentUserManageBlockAsync, applicate agli stessi dati.
        /// </para>
        /// </summary>
        private async Task ApplyListStateAsync(List<TicketDTO> items)
        {
            if (items.Count == 0)
                return;

            var ids = items.Select(x => x.Id).ToList();
            var currentUserId = await _permitsService.IdUser();

            var facts = (await _context.Tickets.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .Select(t => new TicketListFacts
                {
                    Id = t.Id,
                    Closed = t.Closed,
                    Date = t.Date,
                    DateExpired = t.DateExpired,
                    IdState = t.IdState,
                    IdType = t.IdType,
                    IdGroupAssigned = t.IdGroupAssigned,
                    IdUserAssigned = t.IdUserAssigned,
                    IdCommessaFase = t.IdCommessaFase,
                    // Come HasAssignedUserAsync: l'assegnatario storico vale solo se valorizzato
                    // davvero - una stringa vuota non e' un assegnatario.
                    HasAssignedUser = (t.IdUserAssigned != null && t.IdUserAssigned != "") || t.AssignedUsers.Any(),
                    AssignedToCurrentUser = t.IdUserAssigned == currentUserId
                        || t.AssignedUsers.Any(a => a.IdUser == currentUserId),
                    CommessaResponsibleIsCurrentUser = t.CommessaFase != null
                        && t.CommessaFase.Commessa != null
                        && t.CommessaFase.Commessa.IdUserResponsible == currentUserId
                })
                .ToListAsync())
                .ToDictionary(x => x.Id);

            // Utente non risolvibile: niente e' "suo". Senza questo il confronto in query
            // pareggerebbe con i ticket che hanno l'assegnatario a stringa vuota.
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                foreach (var fact in facts.Values)
                {
                    fact.AssignedToCurrentUser = false;
                    fact.CommessaResponsibleIsCurrentUser = false;
                }
            }

            await RefreshExpiredDatesAsync(facts.Values.Where(x => !x.Closed).ToList());

            // Fatti dell'utente: uguali per tutte le righe, quindi chiesti una volta.
            var isClient = await _permitsService.IsClient();
            var canViewInternalData = await _permitsService.CanViewInternalData();
            var isAdminOrSuperUser = await _permitsService.IsAdmin() || await _permitsService.IsSuperUser();
            var belongsToHeadCompany = await _permitsService.BelongsToHeadCompany();

            var myGroupIds = string.IsNullOrWhiteSpace(currentUserId)
                ? new List<int>()
                : await _context.Groups.AsNoTracking()
                    .Where(g => g.Users.Any(u => u.Id == currentUserId))
                    .Select(g => g.Id)
                    .ToListAsync();

            foreach (var item in items)
            {
                if (!facts.TryGetValue(item.Id, out var fact))
                    continue;

                var state = await ResolveListStateAsync(fact, isClient);
                item.IdState = state?.Id;
                item.State = (state?.idState)?.ToString();
                item.StateColor = state?.Color;

                item.Permits = await _permitsService.TicketPermits(
                    item.Id, item.IdCompany, item.IdUserAssigned, fact.AssignedToCurrentUser, fact.Closed);

                item.IsAssignedToCurrentUser = fact.AssignedToCurrentUser;

                item.CanClaim = await CanClaimAsync(
                    fact, currentUserId, isAdminOrSuperUser, belongsToHeadCompany, myGroupIds);

                item.CanManageBlock = await CanManageBlockAsync(
                    item.IdCompany, fact, isAdminOrSuperUser, belongsToHeadCompany);

                ApplyInternalDataVisibility(item, canViewInternalData, isClient);
            }
        }

        /// <summary>
        /// Allinea le scadenze della pagina: stessa regola di CheckTicketExpired, ma per tutti i
        /// ticket in una volta e con una sola scrittura, solo per quelli davvero cambiati.
        /// <para>
        /// I ticket di produzione sono esclusi: la loro scadenza la detta la fase di commessa
        /// (<see cref="ProductionTicketDeadlines"/>), e il calcolo SLA per tipo non conosce il piano.
        /// Senza questa esclusione bastava aprire un elenco per riscrivere la data di consegna del
        /// Gantt con "apertura + N giorni", per giunta in silenzio.
        /// </para>
        /// </summary>
        private async Task RefreshExpiredDatesAsync(List<TicketListFacts> facts)
        {
            var stale = new List<TicketListFacts>();

            foreach (var fact in facts)
            {
                if (fact.IdCommessaFase != null)
                    continue;

                var expected = await ExpectedDateExpiredAsync(fact.IdType, fact.Date);

                if (fact.DateExpired == expected)
                    continue;

                fact.DateExpired = expected;
                stale.Add(fact);
            }

            if (stale.Count == 0)
                return;

            var staleIds = stale.Select(x => x.Id).ToList();
            var tickets = await _context.Tickets.Where(t => staleIds.Contains(t.Id)).ToListAsync();

            foreach (var ticket in tickets)
            {
                ticket.DateExpired = stale.First(x => x.Id == ticket.Id).DateExpired;

                // La scadenza si e' spostata: il preavviso gia' inviato riguardava un'altra data e
                // va rimesso in coda, come fa ProductionTicketDeadlines quando muove le sue.
                ticket.ReminderExpiryStatus = ReminderStatus.Pending;
                ticket.ReminderExpiryRetryCount = 0;
                ticket.ReminderExpiryLastAttemptAt = null;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>Applica <see cref="TicketListRules.ResolveState"/> e traduce in riga di stato.</summary>
        private async Task<TicketState?> ResolveListStateAsync(TicketListFacts fact, bool isClient)
        {
            var state = TicketListRules.ResolveState(
                fact.Closed,
                isClient,
                fact.HasAssignedUser,
                fact.DateExpired,
                DateTime.Now.Date,
                fact.IdUserAssigned != null || fact.IdGroupAssigned != null);

            return await GetIdState(state);
        }

        private async Task<bool> CanClaimAsync(
            TicketListFacts fact,
            string currentUserId,
            bool isAdminOrSuperUser,
            bool belongsToHeadCompany,
            List<int> myGroupIds)
        {
            var hasCurrentUser = !string.IsNullOrWhiteSpace(currentUserId);

            // L'elenco degli assegnabili per tipo costa: si chiede solo se la regola ci arriva,
            // cioe' se il ticket non e' su un gruppo e l'utente non e' Admin.
            var amongTypeAssignees = hasCurrentUser
                && !fact.Closed
                && !fact.AssignedToCurrentUser
                && !isAdminOrSuperUser
                && belongsToHeadCompany
                && fact.IdGroupAssigned == null
                && await IsAmongTypeAssigneesAsync(fact.IdType, currentUserId);

            return TicketListRules.CanClaim(
                hasCurrentUser,
                fact.Closed,
                fact.AssignedToCurrentUser,
                isAdminOrSuperUser,
                belongsToHeadCompany,
                fact.IdGroupAssigned,
                fact.IdGroupAssigned != null && myGroupIds.Contains(fact.IdGroupAssigned.Value),
                amongTypeAssignees);
        }

        private async Task<bool> CanManageBlockAsync(
            int idCompany,
            TicketListFacts fact,
            bool isAdminOrSuperUser,
            bool belongsToHeadCompany)
            => TicketListRules.CanManageBlock(
                await _permitsService.CanGetObject(idCompany),
                isAdminOrSuperUser,
                belongsToHeadCompany,
                fact.AssignedToCurrentUser,
                fact.CommessaResponsibleIsCurrentUser);

        /// <summary>
        /// Gli utenti assegnabili dipendono dal TIPO di ticket, non dal ticket: in una pagina i tipi
        /// distinti sono pochi e l'elenco - che costa parecchie query - si calcola una volta per tipo.
        /// </summary>
        private readonly Dictionary<int, bool> _isAmongTypeAssignees = new();

        private async Task<bool> IsAmongTypeAssigneesAsync(int idType, string currentUserId)
        {
            if (_isAmongTypeAssignees.TryGetValue(idType, out var cached))
                return cached;

            var candidates = await GetUsersCanAssignTicketTypeAsync(idType);
            var result = candidates.Any(u => u.Id == currentUserId);

            _isAmongTypeAssignees[idType] = result;
            return result;
        }

        private static void ApplyInternalDataVisibility(TicketDTO ticket, bool canViewInternalData, bool isClient)
        {
            // Lo smistamento AI e' una decisione interna: la motivazione del modello parla di
            // gruppi e competenze nostre. Servono tutte e due le condizioni, perche' sono assi
            // diversi: il ruolo dice se puoi assegnare, l'azienda madre se sei di casa. Un utente
            // di un'altra azienda puo' avere ruolo Standard e deve restare comunque fuori.
            // Si azzera qui e non solo a video, altrimenti il dato viaggerebbe nel JSON.
            if (!canViewInternalData || isClient)
            {
                ticket.AiSuggestedGroupId = null;
                ticket.AiSuggestedGroup = null;
                ticket.AiRoutingConfidence = null;
                ticket.AiRoutingReason = null;
                ticket.AiRoutedAt = null;
                ticket.AiRoutingApplied = false;
                ticket.AiRoutingOutcome = AiRoutingOutcome.None;
            }

            if (!canViewInternalData)
                ticket.CloseNote = "";
        }

        /// <summary>Cio' che serve sapere di un ticket per deciderne stato e pulsanti.</summary>
        private sealed class TicketListFacts
        {
            public int Id { get; set; }
            public bool Closed { get; set; }
            public DateTime? Date { get; set; }
            public DateTime? DateExpired { get; set; }
            public int? IdState { get; set; }
            public int IdType { get; set; }
            public int? IdGroupAssigned { get; set; }
            public string? IdUserAssigned { get; set; }
            public int? IdCommessaFase { get; set; }
            public bool HasAssignedUser { get; set; }
            public bool AssignedToCurrentUser { get; set; }
            public bool CommessaResponsibleIsCurrentUser { get; set; }
        }

        public async Task SetTicketStateAsync(Ticket ticket)
        {
            TicketState? ticketState = await GetTicketIdState(ticket);
            ticket.IdState = ticketState?.Id;
            ticket.StateDesc = (ticketState?.idState)?.ToString();
            ticket.StateColor = ticketState?.Color;

            ticket.Permits = await _permitsService.TicketPermits(ticket.Id, ticket.IdCompany, ticket.IdUserAssigned);

            if (!await _permitsService.CanViewInternalData())
                ticket.CloseNote = "";
        }

        private async Task<TicketState?> GetTicketIdState(Ticket ticket)
        {
            if (ticket != null)
            {
                if (ticket.Closed)
                    return await GetIdState(eTicketStates.Closed);
                else
                {
                    await CheckTicketExpired(ticket.Id);

                    // Stesse regole di TicketListRules.ResolveState: "in lavorazione" non e' piu'
                    // uno stato a se' dentro l'azienda, un ticket assegnato e' un ticket su cui si
                    // lavora. Resta solo per il cliente, a cui i nostri stati interni non servono.
                    if (await _permitsService.IsClient())
                        return await GetIdState(eTicketStates.Processing);

                    if (ticket.DateExpired != null && DateTime.Now.Date > ticket.DateExpired)
                        return await GetIdState(eTicketStates.Expired);

                    if (ticket.IdUserAssigned != null || ticket.IdGroupAssigned != null)
                        return await GetIdState(eTicketStates.Assigned);

                    return await GetIdState(eTicketStates.Created);
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------------------
        // Memorie di richiesta. Il servizio e' Scoped: la tabella degli stati, i giorni di scadenza
        // per tipo e l'impostazione generale sono configurazione, dentro una richiesta non cambiano.
        // Prima venivano riletti a ogni riga di elenco, anche tre volte per riga.
        // ------------------------------------------------------------------------------------
        private List<TicketState>? _ticketStates;
        private Dictionary<int, int>? _expiredDaysByType;
        private int? _defaultExpiredDays;

        private async Task<List<TicketState>> GetTicketStatesAsync()
            => _ticketStates ??= await _context.TicketStates.AsNoTracking().ToListAsync();

        private async Task<TicketState?> GetIdState(eTicketStates state)
        {
            var ticketState = (await GetTicketStatesAsync()).FirstOrDefault(x => x.State == (int)state);
            if (ticketState != null)
                ticketState.idState = state;
            return ticketState;
        }

        /// <summary>Giorni di scadenza per tipo ticket, con il ripiego sull'impostazione generale.</summary>
        private async Task<int> ExpiredDaysForTypeAsync(int idType)
        {
            if (_expiredDaysByType == null)
            {
                _expiredDaysByType = await _context.TicketTypes.AsNoTracking()
                    .Select(x => new { x.Id, x.ExpiredDate })
                    .ToDictionaryAsync(x => x.Id, x => x.ExpiredDate);
            }

            if (!_expiredDaysByType.TryGetValue(idType, out var days))
                return 3;

            if (days > 0)
                return days;

            _defaultExpiredDays ??= (await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync())?.TicketDaysExpired ?? 3;

            return _defaultExpiredDays.Value;
        }

        /// <summary>
        /// La scadenza che il ticket dovrebbe avere, in base al tipo e alla data. I giorni sono
        /// lavorativi, come alla creazione del ticket: prima qui si contavano solari, quindi il
        /// valore atteso non coincideva mai con quello scritto alla nascita e ogni caricamento di
        /// elenco riscriveva le scadenze - il contrario del motivo per cui ApplyListStateAsync
        /// calcola tutto in blocco.
        /// </summary>
        private async Task<DateTime?> ExpectedDateExpiredAsync(int idType, DateTime? date)
        {
            var days = await ExpiredDaysForTypeAsync(idType);

            return days > 0 && date != null ? date.Value.AddWorkdays(days) : null;
        }

        private async Task<bool> HasAssignedUserAsync(int ticketId, string? legacyAssignedUserId)
        {
            return !string.IsNullOrWhiteSpace(legacyAssignedUserId)
                || await _context.TicketUserAssignments.AnyAsync(a => a.IdTicket == ticketId);
        }

        /// <summary>
        /// Se il richiedente non e' stato indicato, lo ricava dall'utente che sta aprendo il ticket.
        /// La procedura di /Tickets/Create non chiede il contatto, quindi senza questo il ticket
        /// resta senza richiedente: oltre a non vedersi in scheda, gli avvisi di chat finiscono a
        /// tutti gli utenti della ditta invece che a chi ha aperto.
        /// Il contatto viene accettato solo se appartiene alla ditta del ticket: un operatore interno
        /// che apre un ticket per un cliente non deve diventarne il richiedente.
        /// </summary>
        private async Task SetRequesterFromOpenerAsync(Ticket ticket, string idUserOpened)
        {
            if (ticket.IdContact != null || string.IsNullOrWhiteSpace(idUserOpened))
                return;

            var idContact = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == idUserOpened)
                .Select(x => x.IdContact)
                .FirstOrDefaultAsync();

            if (idContact == null)
                return;

            var belongsToTicketCompany = await _context.Contacts
                .AsNoTracking()
                .AnyAsync(x => x.Id == idContact.Value && x.IdCompany == ticket.IdCompany);

            if (belongsToTicketCompany)
                ticket.IdContact = idContact;
        }

        private async Task NormalizeCommessaFaseLinkAsync(Ticket ticket)
        {
            if (ticket.IdCommessaFase == null)
                return;

            var fase = await _context.CommessaFasi
                .AsNoTracking()
                .Where(t => t.Id == ticket.IdCommessaFase)
                .Select(t => new { t.StartDate, t.EndDate })
                .FirstOrDefaultAsync();

            if (fase == null)
            {
                ticket.IdCommessaFase = null;
                return;
            }

            // Collegare un ticket a una fase equivale a prenderla in carico: Standard e Client
            // possono farlo solo sul gruppo a cui appartengono.
            if (!await _commessaFasiService.CanTakeFaseAsync(ticket.IdCommessaFase.Value))
                throw new UnauthorizedAccessException(
                    "Non appartieni al gruppo abilitato a eseguire questa fase di commessa.");

            // ...e la fase non e' avviabile finche' i predecessori non sono completati: le
            // dipendenze sono vincolanti, non un suggerimento grafico.
            var blockers = await _commessaFasiService.GetStartBlockersAsync(ticket.IdCommessaFase.Value);
            if (blockers.Count > 0)
                throw new ProductionSequenceException(
                    $"La fase non e' avviabile: fasi precedenti non completate ({string.Join(", ", blockers)}).");

            // Un ticket di produzione scade con la sua fase e si pianifica dentro la sua finestra:
            // il calcolo SLA per tipo ticket (giorni dall'apertura) non conosce il piano e darebbe
            // date scollegate. Su un ticket gia' chiuso le date sono storia e non si riscrivono.
            if (!ticket.Closed)
                ProductionTicketDeadlines.Apply(ticket, fase.StartDate, fase.EndDate);
        }

        #endregion

        #region Helpers

        public async Task<bool> TicketChangeAssigned(int id, string? idAssigned)
        {
            bool state = false;
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                state = (ticket.IdUserAssigned != idAssigned);
                _context.Entry(ticket).State = EntityState.Detached;
            }

            return state;
        }

        public async Task CheckTicketExpired(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket == null)
                return;

            // Ticket di produzione: la scadenza la detta la fase, non la SLA per tipo.
            if (ticket.IdCommessaFase != null)
                return;

            var expected = await ExpectedDateExpiredAsync(ticket.IdType, ticket.Date);

            // Se la scadenza e' gia' quella giusta non c'e' niente da salvare: prima si passava
            // comunque da SaveChanges, a ogni riga di ogni elenco.
            if (ticket.DateExpired == expected)
                return;

            ticket.DateExpired = expected;
            ticket.ReminderExpiryStatus = ReminderStatus.Pending;
            ticket.ReminderExpiryRetryCount = 0;
            ticket.ReminderExpiryLastAttemptAt = null;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// L'elenco dei lavori di chi sta guardando: quello che apre la mattina per sapere cosa
        /// deve fare.
        /// <para>
        /// Non e' il lavoro "di oggi": e' il lavoro aperto. Quello che non finisce oggi si ritrova
        /// domani. Per l'assistenza la data la decide chi assegna, quindi entrano i ticket di oggi
        /// e quelli arretrati; i ticket di una fase di commessa una data di lavoro non ce l'hanno,
        /// quindi entrano tutti quelli aperti.
        /// </para>
        /// <para>
        /// L'ordine e' a due livelli e per questo l'elenco si costruisce qui e non in una griglia:
        /// prima il gruppo (assistenza, poi commessa, in fondo le fasi bloccate), poi la data
        /// dentro ogni gruppo. Le fasi bloccate stanno in fondo perche' sono lavoro che oggi non
        /// si puo' cominciare: le fasi precedenti non sono finite.
        /// </para>
        /// </summary>
        public async Task<WorkListDTO> GetWorkListAsync()
        {
            try
            {
                var idUser = await _permitsService.IdUser();
                if (string.IsNullOrWhiteSpace(idUser))
                    return new WorkListDTO { ErrorMessage = "Utente non riconosciuto." };

                var myGroupIds = await _context.Groups.AsNoTracking()
                    .Where(g => g.Users.Any(u => u.Id == idUser))
                    .Select(g => g.Id)
                    .ToListAsync();

                var query = await ApplyVisibilityScopeAsync(_context.Tickets.AsNoTracking());

                // Suoi: quelli che ha in mano, piu' quelli fermi sul suo gruppo che nessuno ha
                // ancora preso. Questi ultimi sono lavoro suo a tutti gli effetti: se non li prende
                // lui non li prende nessuno.
                query = query.Where(t => !t.Closed
                    && (t.IdUserAssigned == idUser
                        || t.AssignedUsers.Any(a => a.IdUser == idUser)
                        || (t.IdUserAssigned == null
                            && t.IdGroupAssigned != null
                            && myGroupIds.Contains(t.IdGroupAssigned.Value))));

                // L'assistenza entra se la sua data e' oggi o e' gia' passata: il lavoro futuro
                // non serve stamattina. La produzione entra sempre, non avendo una data di lavoro.
                var fineGiornata = DateTime.Today.AddDays(1);
                query = query.Where(t => t.IdCommessaFase != null
                    || (t.Date != null && t.Date < fineGiornata));

                var righe = await query
                    .Select(t => new
                    {
                        t.Id,
                        t.Date,
                        t.DateExpired,
                        t.Description,
                        t.IdCommessaFase,
                        t.IdUserAssigned,
                        Cliente = t.Company != null ? t.Company.RagioneSociale : string.Empty,
                        FaseName = t.CommessaFase != null ? t.CommessaFase.Name : string.Empty,
                        FaseStato = t.CommessaFase != null ? (CommessaFaseStates?)t.CommessaFase.State : null,
                        FaseFine = t.CommessaFase != null ? (DateTime?)t.CommessaFase.EndDate : null,
                        IdCommessa = t.CommessaFase != null ? (int?)t.CommessaFase.IdCommessa : null,
                        CommessaCode = t.CommessaFase != null && t.CommessaFase.Commessa != null
                            ? (t.CommessaFase.Commessa.Code ?? string.Empty)
                            : string.Empty
                    })
                    .ToListAsync();

                var bloccantiPerFase = await LoadBlockersAsync(righe
                    .Where(r => r.IdCommessaFase != null && r.FaseStato == CommessaFaseStates.Pending)
                    .Select(r => r.IdCommessaFase!.Value)
                    .Distinct()
                    .ToList());

                var oggi = DateTime.Today;
                var items = righe.Select(r =>
                {
                    var bloccanti = r.IdCommessaFase != null
                        && bloccantiPerFase.TryGetValue(r.IdCommessaFase.Value, out var nomi)
                            ? nomi
                            : new List<string>();

                    var gruppo = r.IdCommessaFase == null
                        ? WorkListGroup.Assistenza
                        : bloccanti.Count > 0 ? WorkListGroup.CommessaBloccata : WorkListGroup.Commessa;

                    return new WorkListItemDTO
                    {
                        IdTicket = r.Id,
                        Group = gruppo,
                        Numero = r.Id.ToString("000000"),
                        Descrizione = r.Description ?? string.Empty,
                        Cliente = r.Cliente,
                        Data = r.Date,
                        // Sulla produzione la scadenza e' la fine della fase: se sul ticket manca,
                        // si legge dalla fase invece di lasciare la riga senza riferimento.
                        Scadenza = r.DateExpired ?? r.FaseFine,
                        CommessaCode = r.CommessaCode,
                        IdCommessa = r.IdCommessa,
                        FaseName = r.FaseName,
                        DaPrendere = r.IdUserAssigned == null,
                        BloccatoDa = bloccanti
                    };
                }).ToList();

                foreach (var item in items)
                    item.InRitardo = item.Scadenza != null && item.Scadenza.Value.Date < oggi;

                // Il criterio di ordinamento dentro il gruppo: l'assistenza per la data che le ha
                // dato chi assegna, la produzione per la scadenza della fase.
                return new WorkListDTO
                {
                    Items = items
                        .OrderBy(i => (int)i.Group)
                        .ThenBy(i => i.Group == WorkListGroup.Assistenza
                            ? i.Data ?? DateTime.MaxValue
                            : i.Scadenza ?? DateTime.MaxValue)
                        .ThenBy(i => i.IdTicket)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetWorkListAsync), EventsTypes.Error, ex);
                return new WorkListDTO { ErrorMessage = "Impossibile caricare l'elenco dei lavori." };
            }
        }

        /// <summary>
        /// Per ogni fase indicata, i nomi delle fasi precedenti non ancora finite. Stessa regola di
        /// CommessaFasiService.GetStartBlockersAsync, chiesta pero' per tutte le fasi in una volta:
        /// una query per riga d'elenco sarebbe una interrogazione per ogni lavoro mostrato.
        /// </summary>
        private async Task<Dictionary<int, List<string>>> LoadBlockersAsync(List<int> faseIds)
        {
            if (faseIds.Count == 0)
                return new Dictionary<int, List<string>>();

            var dipendenze = await _context.CommessaFaseDependencies
                .AsNoTracking()
                .Where(d => faseIds.Contains(d.IdFase)
                    && d.PredecessorFase != null
                    && d.PredecessorFase.State != CommessaFaseStates.Done)
                .Select(d => new { d.IdFase, Nome = d.PredecessorFase!.Name })
                .ToListAsync();

            return dipendenze
                .GroupBy(d => d.IdFase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Nome).ToList());
        }

        public async Task<int> GetDayBeforeExpired(int id)
        {
            var idType = await _context.Tickets.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => (int?)x.IdType)
                .FirstOrDefaultAsync();

            return idType == null ? 3 : await ExpiredDaysForTypeAsync(idType.Value);
        }

        /// <summary>
        /// Perimetro di visibilita' dei ticket, per azienda.
        /// Chi appartiene all'azienda madre vede tutti i ticket, qualunque sia il ruolo: per lui
        /// il ruolo limita le AZIONI (service, chat, chiusura, auto-assegnazione), non la vista.
        /// Chi non vi appartiene vede solo la propria azienda; se e' un rivenditore, anche quelle
        /// figlie ricorsivamente. Entrambi i casi sono gia' risolti da GetVisibleCompanyIds:
        /// restituisce null per l'azienda madre e l'albero delle aziende per il rivenditore.
        /// Da non sostituire con CanAccessOtherCompany, che e' true anche per i rivenditori e
        /// lascerebbe loro vedere i ticket di tutte le aziende.
        /// </summary>
        private async Task<IQueryable<Ticket>> ApplyVisibilityScopeAsync(IQueryable<Ticket> tickets)
        {
            var allowed = await _permitsService.GetVisibleCompanyIds();

            if (allowed == null)
                return tickets;

            return tickets.Where(x => allowed.Contains(x.IdCompany));
        }

        private IQueryable<Ticket> FilterByType(IQueryable<Ticket> tickets, TicketTypeSearch filter, string idUser, bool isClient)
        {
            switch (filter)
            {
                // Un gruppo assegnato e' un'assegnazione a tutti gli effetti: stesso criterio
                // dello stato del ticket (SetTicketStateAsync) e del contatore della dashboard.
                // Cio' che manca a un ticket di gruppo e' il responsabile: vedi ToClaim.
                case TicketTypeSearch.Assigned:
                    tickets = tickets.Where(x => !x.Closed);
                    tickets = tickets.Where(x => x.IdUserAssigned != null || x.IdGroupAssigned != null);
                    break;

                case TicketTypeSearch.NotAssigned:
                    tickets = tickets.Where(x => !x.Closed);
                    tickets = tickets.Where(x => x.IdUserAssigned == null && x.IdGroupAssigned == null);
                    break;

                case TicketTypeSearch.ToClaim:
                    tickets = tickets.Where(x => !x.Closed && x.IdUserAssigned == null && x.IdGroupAssigned != null);
                    break;

                case TicketTypeSearch.Expired:
                    tickets = tickets.Where(x => !x.Closed);
                    DateTime date = DateTime.Now.Date;
                    tickets = tickets.Where(x => date > x.DateExpired);
                    break;

                case TicketTypeSearch.NewMessage:
                    tickets = tickets.Where(x => x.TicketsChats.Where(y => y.TicketChatReads.Where(z => z.Displayed == false && z.IdUser == idUser).Any()).Any());
                    break;

                case TicketTypeSearch.ToBeInvoiced:
                    tickets = tickets.Where(x => x.Invoiced == false);
                    break;

                case TicketTypeSearch.Closed:
                    tickets = tickets.Where(x => x.Closed);
                    break;

                case TicketTypeSearch.Blocked:
                    tickets = tickets.Where(x => !x.Closed && x.IsBlocked);
                    break;
            }

            return tickets;
        }

        private async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ReloadTicketResponseAsync(int idTicket, string message)
        {
            return new CRM.Client.Models.APIResponseMessage<TicketDTO>
            {
                State = true,
                Code = System.Net.HttpStatusCode.OK,
                Message = message,
                Data = await GetDetailsAsync(idTicket)
            };
        }

        private static CRM.Client.Models.APIResponseMessage<TicketDTO> FailTicket(string message, System.Net.HttpStatusCode code)
        {
            return new CRM.Client.Models.APIResponseMessage<TicketDTO>
            {
                State = false,
                Code = code,
                Message = message
            };
        }

        #endregion

        #region Assignment

        public async Task<List<string>> GetAssignedUserIdsAsync(int idTicket)
        {
            try
            {
                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null)
                    return new List<string>();

                var userIds = ticket.AssignedUsers
                    .Select(au => au.IdUser)
                    .Where(idUser => !string.IsNullOrWhiteSpace(idUser))
                    .ToList();

                if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned))
                    userIds.Add(ticket.IdUserAssigned);

                return userIds
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetAssignedUserIdsAsync), EventsTypes.Error, ex);
                return new List<string>();
            }
        }

        public async Task<TicketAssignmentContextDTO?> GetAssignmentContextAsync(int idTicket)
        {
            try
            {
                // Proiezione e non entita': il nome del gruppo arriva senza trascinare Group -> Users.
                var ticket = await _context.Tickets
                    .AsNoTracking()
                    .Where(t => t.Id == idTicket)
                    .Select(t => new
                    {
                        t.Id,
                        t.IdGroupAssigned,
                        GroupName = t.GroupAssigned != null ? t.GroupAssigned.Name : null,
                        t.Closed,
                        t.IdUserAssigned,
                        AssignedUserIds = t.AssignedUsers.Select(a => a.IdUser).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (ticket == null)
                    return null;

                // Il campo legacy IdUserAssigned vale come assegnazione, come in GetAssignedUserIdsAsync.
                var assignedUserIds = ticket.AssignedUserIds
                    .Where(idUser => !string.IsNullOrWhiteSpace(idUser))
                    .ToList();

                if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned))
                    assignedUserIds.Add(ticket.IdUserAssigned);

                return new TicketAssignmentContextDTO
                {
                    IdTicket = ticket.Id,
                    IdGroupAssigned = ticket.IdGroupAssigned,
                    GroupAssigned = ticket.GroupName,
                    Closed = ticket.Closed,
                    AssignedUserIds = assignedUserIds.Distinct().ToList()
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetAssignmentContextAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<AssignUsersResult> AssignUsersAsync(int idTicket, AssignUsersRequest request, string? currentUserId)
        {
            try
            {
                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null)
                {
                    return new AssignUsersResult { Success = false, ErrorMessage = $"Ticket con ID {idTicket} non trovato" };
                }

                var previouslyAssignedUserIds = ticket.AssignedUsers
                    .Select(au => au.IdUser)
                    .ToHashSet();

                _context.TicketUserAssignments.RemoveRange(ticket.AssignedUsers);

                var newlyAssignedUserIds = new HashSet<string>();

                if (request.UserIds != null && request.UserIds.Any())
                {
                    foreach (var userId in request.UserIds)
                    {
                        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                        if (!userExists)
                        {
                            return new AssignUsersResult { Success = false, ErrorMessage = $"Utente con ID {userId} non trovato" };
                        }

                        var assignment = new TicketUserAssignment
                        {
                            IdTicket = idTicket,
                            IdUser = userId,
                            AssignedDate = DateTime.Now,
                            AssignedBy = currentUserId
                        };

                        _context.TicketUserAssignments.Add(assignment);
                        newlyAssignedUserIds.Add(userId);
                    }

                    ticket.IdUserAssigned = request.UserIds.First();
                }
                else
                {
                    ticket.IdUserAssigned = null;
                }

                await ApplyAssignmentStateAsync(ticket);

                await _context.SaveChangesAsync();

                var addedUsers = newlyAssignedUserIds.Except(previouslyAssignedUserIds).ToList();
                var removedUsers = previouslyAssignedUserIds.Except(newlyAssignedUserIds).ToList();

                await _logEventService.RegisterAsync(
                    nameof(TicketsService),
                    nameof(AssignUsersAsync),
                    EventsTypes.Info,
                    $"Ticket #{idTicket}: {(request.UserIds?.Any() == true ? $"Assegnati {request.UserIds.Count} utenti" : "Rimosse tutte le assegnazioni")}");

                return new AssignUsersResult
                {
                    Success = true,
                    AddedUserIds = addedUsers,
                    RemovedUserIds = removedUsers,
                    AssignedCount = request.UserIds?.Count ?? 0,
                    Ticket = ticket
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(AssignUsersAsync), EventsTypes.Error, ex);
                return new AssignUsersResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ClaimAsync(int idTicket, string? currentUserId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentUserId))
                    return FailClaim("Utente non autenticato", System.Net.HttpStatusCode.Unauthorized);

                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .Include(t => t.GroupAssigned)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null)
                    return FailClaim("Ticket non trovato", System.Net.HttpStatusCode.NotFound);

                if (!await _permitsService.CanGetObject(ticket.IdCompany))
                    return FailClaim("Ticket non accessibile", System.Net.HttpStatusCode.Forbidden);

                if (ticket.Closed)
                    return FailClaim("Un ticket chiuso non puo' essere preso in carico", System.Net.HttpStatusCode.BadRequest);

                if (ticket.AssignedUsers.Any(a => a.IdUser == currentUserId) || ticket.IdUserAssigned == currentUserId)
                    return await OkClaim(idTicket, "Ticket gia' in carico a te");

                if (!await CanCurrentUserClaimTicketAsync(ticket, currentUserId))
                    return FailClaim("Non appartieni al gruppo assegnato a questo ticket", System.Net.HttpStatusCode.Forbidden);

                _context.TicketUserAssignments.Add(new TicketUserAssignment
                {
                    IdTicket = ticket.Id,
                    IdUser = currentUserId,
                    AssignedDate = DateTime.Now,
                    AssignedBy = currentUserId
                });

                ticket.IdUserAssigned ??= currentUserId;
                await ApplyAssignmentStateAsync(ticket);

                await _context.SaveChangesAsync();
                return await OkClaim(idTicket, "Ticket preso in carico");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(ClaimAsync), EventsTypes.Error, ex);
                return FailClaim("Errore nella presa in carico del ticket", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<EnsureAssignedResult> EnsureUsersAssignedAsync(int idTicket, IEnumerable<string> userIds, string? currentUserId)
        {
            try
            {
                var requested = (userIds ?? Enumerable.Empty<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null)
                    return FailEnsure(EnsureAssignedError.TicketNotFound, $"Ticket con ID {idTicket} non trovato");

                if (!await _permitsService.CanGetObject(ticket.IdCompany))
                    return FailEnsure(EnsureAssignedError.Forbidden, "Ticket non accessibile");

                var alreadyAssigned = ticket.AssignedUsers
                    .Select(a => a.IdUser)
                    .ToHashSet();

                if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned))
                    alreadyAssigned.Add(ticket.IdUserAssigned);

                var toClaim = requested
                    .Where(id => !alreadyAssigned.Contains(id))
                    .ToList();

                if (toClaim.Count == 0)
                    return new EnsureAssignedResult { Success = true, Ticket = ticket };

                var claimedNames = new List<string>();

                foreach (var idUser in toClaim)
                {
                    // Contact serve a NameComplete: senza, il messaggio all'operatore mostrerebbe la mail.
                    var user = await _context.Users
                        .AsNoTracking()
                        .Include(u => u.Contact)
                        .FirstOrDefaultAsync(u => u.Id == idUser);

                    if (user == null)
                        return FailEnsure(EnsureAssignedError.UserNotEligible, $"Utente con ID {idUser} non trovato");

                    if (!await CanUserWorkOnTicketAsync(ticket, idUser))
                        return FailEnsure(EnsureAssignedError.UserNotEligible,
                            $"{user.NameComplete} non puo' lavorare sul ticket #{idTicket}: non appartiene al gruppo assegnato ne' agli utenti abilitati sul tipo di ticket.");

                    _context.TicketUserAssignments.Add(new TicketUserAssignment
                    {
                        IdTicket = ticket.Id,
                        IdUser = idUser,
                        AssignedDate = DateTime.Now,
                        AssignedBy = currentUserId
                    });

                    claimedNames.Add(user.NameComplete);
                }

                ticket.IdUserAssigned ??= toClaim.First();

                // Su un ticket chiuso l'assegnazione resta solo una traccia di chi ha lavorato:
                // cambiarne lo stato lo riaprirebbe di fatto. Su un ticket aperto, invece,
                // assegnare qualcuno significa solo "assegnato": la lavorazione vera parte dal
                // comando esplicito StartProcessing.
                await ApplyAssignmentStateAsync(ticket);

                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(
                    nameof(TicketsService),
                    nameof(EnsureUsersAssignedAsync),
                    EventsTypes.Info,
                    $"Ticket #{idTicket}: presa in carico implicita di {claimedNames.Count} utenti ({string.Join(", ", claimedNames)})");

                return new EnsureAssignedResult
                {
                    Success = true,
                    ClaimedUserIds = toClaim,
                    ClaimedUserNames = claimedNames,
                    Ticket = ticket
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(EnsureUsersAssignedAsync), EventsTypes.Error, ex);
                return FailEnsure(EnsureAssignedError.Unexpected, "Errore nell'assegnazione degli utenti al ticket");
            }
        }

        private static EnsureAssignedResult FailEnsure(EnsureAssignedError error, string message)
            => new() { Success = false, Error = error, ErrorMessage = message };

        public async Task<bool> StartWorkAsync(int idTicket, IEnumerable<string>? userIds, string? currentUserId)
        {
            try
            {
                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null || ticket.Closed)
                    return false;

                var workUserIds = (userIds ?? Enumerable.Empty<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                if (workUserIds.Count == 0 && !string.IsNullOrWhiteSpace(currentUserId))
                    workUserIds.Add(currentUserId);

                foreach (var idUser in workUserIds)
                {
                    if (ticket.AssignedUsers.Any(a => a.IdUser == idUser) || ticket.IdUserAssigned == idUser)
                        continue;

                    _context.TicketUserAssignments.Add(new TicketUserAssignment
                    {
                        IdTicket = ticket.Id,
                        IdUser = idUser,
                        AssignedDate = DateTime.Now,
                        AssignedBy = currentUserId
                    });

                    ticket.IdUserAssigned ??= idUser;
                }

                if (!HasAssignedUserInMemory(ticket))
                    return false;

                // Registrare un intervento assegna chi ci ha lavorato, e basta. Prima portava il
                // ticket in uno stato "in lavorazione" a se': quello stato non esiste piu' dentro
                // l'azienda, perche' un ticket assegnato e' gia' un ticket su cui si lavora.
                await ApplyAssignmentStateAsync(ticket);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(StartWorkAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        /// <summary>
        /// Lo stato segue l'assegnazione: assegnato a qualcuno o a un gruppo, il ticket e'
        /// "assegnato"; senza nessuno sopra torna "aperto". Su un ticket chiuso non si tocca niente,
        /// perche' cambiargli stato lo riaprirebbe di fatto.
        /// <para>
        /// Prima qui si preservava anche "in lavorazione". Quello stato non esiste piu' dentro
        /// l'azienda: un ticket assegnato e' un ticket su cui si lavora, e distinguere le due cose
        /// raccontava chi aveva premuto quale pulsante invece del punto a cui e' il lavoro.
        /// </para>
        /// </summary>
        private async Task ApplyAssignmentStateAsync(Ticket ticket)
        {
            if (ticket.Closed)
                return;

            var target = HasAnyAssigneeInMemory(ticket)
                ? eTicketStates.Assigned
                : eTicketStates.Created;

            ticket.IdState = (await GetIdState(target))?.Id ?? ticket.IdState;
        }

        private bool HasAssignedUserInMemory(Ticket ticket)
            => !string.IsNullOrWhiteSpace(ticket.IdUserAssigned)
                || (ticket.AssignedUsers?.Any(a =>
                    !string.IsNullOrWhiteSpace(a.IdUser)
                    && _context.Entry(a).State != EntityState.Deleted) ?? false);

        private bool HasAnyAssigneeInMemory(Ticket ticket)
            => HasAssignedUserInMemory(ticket) || ticket.IdGroupAssigned != null;

        /// <summary>
        /// Un utente puo' lavorare sul ticket se e' nel gruppo a cui e' smistato oppure se e' fra
        /// gli abilitati sul tipo di ticket (regola gia' usata per l'assegnazione manuale).
        /// </summary>
        private async Task<bool> CanUserWorkOnTicketAsync(Ticket ticket, string idUser)
        {
            if (ticket.IdGroupAssigned != null)
            {
                var belongsToGroup = await _context.Groups
                    .AsNoTracking()
                    .AnyAsync(g => g.Id == ticket.IdGroupAssigned.Value && g.Users.Any(u => u.Id == idUser));

                if (belongsToGroup)
                    return true;
            }

            return await _permitsService.CanReceveTicket(ticket.Id, idUser);
        }

        private async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> OkClaim(int idTicket, string message)
            => new()
            {
                State = true,
                Code = System.Net.HttpStatusCode.OK,
                Message = message,
                Data = await GetDetailsAsync(idTicket)
            };

        private static CRM.Client.Models.APIResponseMessage<TicketDTO> FailClaim(string message, System.Net.HttpStatusCode code)
            => new() { State = false, Code = code, Message = message };

        private async Task<bool> CanCurrentUserClaimTicketAsync(Ticket ticket, string currentUserId)
        {
            if (ticket.Closed)
                return false;

            if (ticket.IdUserAssigned == currentUserId
                || await _context.TicketUserAssignments.AnyAsync(a => a.IdTicket == ticket.Id && a.IdUser == currentUserId))
                return false;

            if (await _permitsService.IsAdmin() || await _permitsService.IsSuperUser())
                return true;

            if (!await _permitsService.BelongsToHeadCompany())
                return false;

            if (ticket.IdGroupAssigned != null)
            {
                return await _context.Groups
                    .AsNoTracking()
                    .AnyAsync(g => g.Id == ticket.IdGroupAssigned.Value && g.Users.Any(u => u.Id == currentUserId));
            }

            var candidates = await GetUsersCanAssignTicketAsync(ticket.Id);
            return candidates.Any(u => u.Id == currentUserId);
        }

        #endregion
    }
}
