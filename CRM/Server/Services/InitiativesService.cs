using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Iniziative: fiere, trasferte, campagne. Contenitore di costo e di attribuzione per tutto
    /// cio' che nasce da una stessa occasione.
    /// </summary>
    public class InitiativesService : IInitiativesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public InitiativesService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        /// <summary>
        /// Colonne della griglia tradotte nei percorsi dell'entita': la griglia ordina per i nomi
        /// del DTO, qui si interroga l'entita'. Quello che non c'e' in mappa e non e' una proprieta'
        /// dell'entita' viene scartato, non fatto esplodere.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> SortableColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OwnerName"] = "Owner.NameComplete"
            };

        public async Task<InitiativeDTO?> GetItemAsync(int id)
        {
            try
            {
                var item = await BaseQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (item == null)
                    return null;

                var dto = item.ToDTO()!;
                dto.Permits = ComputePermits(item.IdOwner, await SafeCurrentUserAsync());
                await FillCountersAsync(new List<InitiativeDTO> { dto });
                return dto;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(GetItemAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<InitiativeDTO, decimal>?> GetSummaryListAsync(InitiativeFilter? args)
        {
            try
            {
                var q = FilterItems(args);
                var count = await q.CountAsync();
                var budget = await q.SumAsync(x => x.BudgetPlanned ?? 0);

                if (args?.Skip != null && args.Top != null)
                {
                    q = q.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var items = await ToDtosAsync(q);

                return new PagingResponse<InitiativeDTO, decimal>
                {
                    Items = items,
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args?.PageSize ?? 0 },
                    Total = budget
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(GetSummaryListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<InitiativeDTO>?> GetListAsync(InitiativeFilter? args = null)
        {
            try
            {
                return await ToDtosAsync(FilterItems(args));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<InitiativeDTO>> PostAsync(Initiative item)
        {
            try
            {
                if (item.DateTo < item.DateFrom)
                    return Fail("La data di fine e' precedente a quella di inizio", System.Net.HttpStatusCode.BadRequest);

                List<string> newMembers;

                if (item.Id == 0)
                {
                    item.CreatedAt = item.CreatedAt == default ? DateTime.Now : item.CreatedAt;
                    item.IdOwner = string.IsNullOrWhiteSpace(item.IdOwner) ? await SafeCurrentUserAsync() : item.IdOwner;

                    var requested = item.Members.ToList();
                    item.Members.Clear();
                    newMembers = await ReconcileMembersAsync(item, requested);

                    _context.Initiatives.Add(item);
                }
                else
                {
                    var existing = await _context.Initiatives
                        .Include(x => x.Members)
                        .FirstOrDefaultAsync(x => x.Id == item.Id);

                    if (existing == null)
                        return Fail("Iniziativa non trovata", System.Net.HttpStatusCode.NotFound);

                    existing.Name = item.Name;
                    existing.Kind = item.Kind;
                    existing.State = item.State;
                    existing.Location = item.Location;
                    existing.DateFrom = item.DateFrom;
                    existing.DateTo = item.DateTo;
                    existing.BudgetPlanned = item.BudgetPlanned;
                    existing.Objective = item.Objective;
                    existing.ClosingNotes = item.ClosingNotes;
                    existing.IdOwner = string.IsNullOrWhiteSpace(item.IdOwner) ? existing.IdOwner : item.IdOwner;
                    newMembers = await ReconcileMembersAsync(existing, item.Members);
                    item = existing;
                }

                // Prima scrittura: serve l'Id dell'iniziativa (e dei membri) per appenderci le
                // presenze, che vanno quindi in una seconda passata.
                await _context.SaveChangesAsync();

                await CreateDefaultSchedulesAsync(item, newMembers);
                await _context.SaveChangesAsync();

                return Ok((await GetItemAsync(item.Id))!, "Iniziativa salvata");
            }
            catch (Exception ex)
            {
                // Il contesto ha ancora le modifiche fallite in sospeso: senza scartarle il log,
                // che scrive sullo stesso contesto, riproverebbe la stessa scrittura e rilancerebbe
                // la stessa eccezione, facendo sparire l'errore invece di registrarlo.
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio dell'iniziativa", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<InitiativeDTO>> CloseAsync(int id, string? closingNotes)
        {
            try
            {
                var item = await _context.Initiatives.FirstOrDefaultAsync(x => x.Id == id);
                if (item == null)
                    return Fail("Iniziativa non trovata", System.Net.HttpStatusCode.NotFound);

                item.State = InitiativeState.Closed;
                item.ClosedAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(closingNotes))
                    item.ClosingNotes = closingNotes;

                await _context.SaveChangesAsync();
                return Ok((await GetItemAsync(id))!, "Iniziativa chiusa");
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(CloseAsync), EventsTypes.Error, ex);
                return Fail("Errore nella chiusura dell'iniziativa", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        // ------------------------------------------------------------------------------------
        // Triage dei biglietti raccolti
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Domini di posta che non dicono nulla sull'azienda: due indirizzi gmail non sono un
        /// indizio di niente, e proporre un collegamento su quella base farebbe piu' danni che
        /// lasciare il campo vuoto.
        /// </summary>
        private static readonly HashSet<string> GenericMailDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "googlemail.com", "hotmail.com", "hotmail.it", "outlook.com", "outlook.it",
            "live.com", "live.it", "yahoo.com", "yahoo.it", "icloud.com", "libero.it", "alice.it",
            "virgilio.it", "tin.it", "tiscali.it", "fastwebnet.it", "pec.it"
        };

        /// <summary>
        /// Sigle societarie, gia' compattate: due ragioni sociali non si distinguono per la forma
        /// giuridica. Ordinate dalla piu' lunga cosi' "srls" non viene tagliato come "srl" e
        /// "limited" non come "ltd".
        /// </summary>
        private static readonly string[] CompanySuffixes =
            new[] { "srls", "srl", "spa", "snc", "sas", "sarl", "gmbh", "limited", "ltd", "plc", "inc", "llc", "bv", "nv", "ag", "sa", "sc", "oy", "ab" }
                .OrderByDescending(s => s.Length)
                .ToArray();

        public async Task<List<InitiativeLeadTriageDTO>> GetLeadTriageAsync(int id)
        {
            try
            {
                var leads = await _context.Leads
                    .AsNoTracking()
                    .Where(l => l.IdInitiative == id)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new InitiativeLeadTriageDTO
                    {
                        Id = l.Id,
                        Name = l.Name,
                        CompanyName = l.CompanyName,
                        Email = l.Email,
                        Phone = l.Phone,
                        Note = l.Note,
                        Score = l.Score,
                        Status = l.Status,
                        CreatedAt = l.CreatedAt,
                        HasBusinessCard = l.IdBusinessCard != null,
                        IdCompany = l.IdCompany
                    })
                    .ToListAsync();

                if (leads.Count == 0)
                    return leads;

                var companies = await _context.Companies
                    .AsNoTracking()
                    .Select(c => new { c.Id, c.RagioneSociale, c.Email })
                    .ToListAsync();

                // Un contatto con la stessa email e' l'indizio piu' forte: e' la persona, non
                // un'omonimia di ragione sociale.
                var contactEmails = await _context.Contacts
                    .AsNoTracking()
                    .Where(c => c.IdCompany != null && c.Email != null && c.Email != string.Empty)
                    .Select(c => new { c.Email, c.IdCompany })
                    .ToListAsync();

                var byContactEmail = contactEmails
                    .GroupBy(c => c.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().IdCompany!.Value, StringComparer.OrdinalIgnoreCase);

                var byCompanyEmail = companies
                    .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                    .GroupBy(c => c.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var byCompanyDomain = companies
                    .Select(c => new { Company = c, Domain = MailDomain(c.Email) })
                    .Where(x => x.Domain != null && !GenericMailDomains.Contains(x.Domain))
                    .GroupBy(x => x.Domain!, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() == 1)   // dominio condiviso da piu' aziende: non decide nulla
                    .ToDictionary(g => g.Key, g => g.First().Company, StringComparer.OrdinalIgnoreCase);

                var byNormalizedName = companies
                    .Select(c => new { Company = c, Key = NormalizeCompanyName(c.RagioneSociale) })
                    .Where(x => x.Key.Length >= 3)
                    .GroupBy(x => x.Key, StringComparer.Ordinal)
                    .Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.First().Company, StringComparer.Ordinal);

                foreach (var lead in leads)
                {
                    FillMissing(lead);

                    if (lead.IdCompany != null)
                        continue;   // gia' collegato: non c'e' niente da proporre

                    var email = lead.Email?.Trim();
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        if (byContactEmail.TryGetValue(email, out var idCompany))
                        {
                            var match = companies.FirstOrDefault(c => c.Id == idCompany);
                            if (match != null)
                            {
                                Suggest(lead, match.Id, match.RagioneSociale, "stessa email di un contatto");
                                continue;
                            }
                        }

                        if (byCompanyEmail.TryGetValue(email, out var byMail))
                        {
                            Suggest(lead, byMail.Id, byMail.RagioneSociale, "stessa email dell'azienda");
                            continue;
                        }

                        var domain = MailDomain(email);
                        if (domain != null
                            && !GenericMailDomains.Contains(domain)
                            && byCompanyDomain.TryGetValue(domain, out var byDomain))
                        {
                            Suggest(lead, byDomain.Id, byDomain.RagioneSociale, $"stesso dominio email (@{domain})");
                            continue;
                        }
                    }

                    var normalized = NormalizeCompanyName(lead.CompanyName);
                    if (normalized.Length >= 3 && byNormalizedName.TryGetValue(normalized, out var byName))
                        Suggest(lead, byName.Id, byName.RagioneSociale, "ragione sociale corrispondente");
                }

                return leads;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(GetLeadTriageAsync), EventsTypes.Error, ex);
                return new List<InitiativeLeadTriageDTO>();
            }
        }

        private static void Suggest(InitiativeLeadTriageDTO lead, int idCompany, string name, string reason)
        {
            lead.SuggestedCompanyId = idCompany;
            lead.SuggestedCompanyName = name;
            lead.SuggestionReason = reason;
        }

        /// <summary>
        /// Cosa manca per poter lavorare il contatto. "Cosa voleva" e' in elenco di proposito: e'
        /// l'unica cosa che allo stand poteva scrivere solo chi c'era, e domani non la ricostruisce
        /// piu' nessuno.
        /// </summary>
        private static void FillMissing(InitiativeLeadTriageDTO lead)
        {
            if (string.IsNullOrWhiteSpace(lead.Email) && string.IsNullOrWhiteSpace(lead.Phone))
                lead.Missing.Add("recapito");

            if (string.IsNullOrWhiteSpace(lead.CompanyName) && lead.IdCompany == null)
                lead.Missing.Add("azienda");

            if (string.IsNullOrWhiteSpace(lead.Note))
                lead.Missing.Add("cosa voleva");

            // Il segnaposto della cattura rapida: si e' salvato il biglietto senza riuscire a
            // leggerne il nome, e la foto e' li' che aspetta di essere trascritta.
            if (lead.Name.StartsWith("Biglietto delle", StringComparison.OrdinalIgnoreCase))
                lead.Missing.Add("nome");
        }

        private static string? MailDomain(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            var at = email.LastIndexOf('@');
            return at > 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
        }

        /// <summary>
        /// Ragione sociale ridotta al confrontabile: niente maiuscole, niente punteggiatura,
        /// niente forma giuridica. "Muller S.r.l." e "MULLER SRL" sono la stessa azienda.
        /// </summary>
        private static string NormalizeCompanyName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Si toglie TUTTO cio' che non e' lettera o cifra, spazi compresi: e' l'unico modo per
            // far combaciare "Muller S.r.l." e "MULLER SRL", che a parole restano diverse perche'
            // i puntini spezzano la sigla in tre lettere sciolte.
            var compact = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

            foreach (var suffix in CompanySuffixes)
            {
                // La sigla si toglie solo dalla coda e solo se resta abbastanza nome: "Muller SRL"
                // e "Muller" sono la stessa azienda, "SR Logistica" non e' "Logistica".
                if (compact.Length > suffix.Length + 2 && compact.EndsWith(suffix, StringComparison.Ordinal))
                {
                    compact = compact[..^suffix.Length];
                    break;
                }
            }

            return compact;
        }

        public async Task<bool> LinkLeadToCompanyAsync(int id, int idLead, int idCompany)
        {
            try
            {
                var lead = await _context.Leads.FirstOrDefaultAsync(x => x.Id == idLead && x.IdInitiative == id);
                if (lead == null)
                    return false;

                var company = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.Id == idCompany)
                    .Select(c => new { c.Id, c.RagioneSociale })
                    .FirstOrDefaultAsync();

                if (company == null)
                    return false;

                lead.IdCompany = company.Id;

                // La ragione sociale scritta sul biglietto viene sostituita da quella dell'anagrafica:
                // da qui in poi la verita' e' l'azienda, non come l'aveva stampata il cartoncino.
                lead.CompanyName = company.RagioneSociale;
                lead.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(LinkLeadToCompanyAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        /// <summary>
        /// Chi risulta impegnato in un'iniziativa nel periodo indicato. Si legge dalle presenze e
        /// non dai membri: e' la differenza fra "fa parte della squadra" e "quel giorno non c'e'".
        /// </summary>
        public async Task<List<UserAwayDTO>> GetAwayUsersAsync(DateTime from, DateTime to)
        {
            try
            {
                return await _context.InitiativeSchedules
                    .AsNoTracking()
                    .Include(s => s.Initiative)
                    .Where(s => s.Start < to
                                && s.End > from
                                && s.Initiative != null
                                && s.Initiative.State != InitiativeState.Cancelled)
                    .OrderBy(s => s.Start)
                    .Select(s => new UserAwayDTO
                    {
                        IdUser = s.IdUser,
                        IdInitiative = s.IdInitiative,
                        InitiativeName = s.Initiative!.Name,
                        Kind = s.Initiative.Kind,
                        Start = s.Start,
                        End = s.End,
                        Location = s.Location ?? s.Initiative.Location
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(GetAwayUsersAsync), EventsTypes.Error, ex);
                return new List<UserAwayDTO>();
            }
        }

        public async Task<List<InitiativeScheduleDTO>> GetSchedulesAsync(int id)
        {
            return await _context.InitiativeSchedules
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.IdInitiative == id)
                .OrderBy(x => x.Start)
                .Select(x => new InitiativeScheduleDTO
                {
                    Id = x.Id,
                    IdInitiative = x.IdInitiative,
                    IdUser = x.IdUser,
                    UserName = x.User != null ? x.User.NameComplete : string.Empty,
                    Start = x.Start,
                    End = x.End,
                    Type = x.Type,
                    Location = x.Location,
                    Notes = x.Notes,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<APIResponseMessage<InitiativeScheduleDTO>> SaveScheduleAsync(int id, InitiativeScheduleDTO schedule)
        {
            try
            {
                if (schedule.End <= schedule.Start)
                    return FailSchedule("La fine deve essere successiva all'inizio", System.Net.HttpStatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(schedule.IdUser))
                    return FailSchedule("Seleziona un utente", System.Net.HttpStatusCode.BadRequest);

                var initiative = await _context.Initiatives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (initiative == null)
                    return FailSchedule("Iniziativa non trovata", System.Net.HttpStatusCode.NotFound);

                if (schedule.Start.Date < initiative.DateFrom.Date || schedule.End.Date > initiative.DateTo.Date)
                    return FailSchedule("La presenza deve rientrare nel periodo dell'iniziativa", System.Net.HttpStatusCode.BadRequest);

                // Due presenze accavallate sulla stessa persona diventano due blocchi sovrapposti
                // sullo stesso nome in agenda: chi guarda non capisce quale valga, e la risposta a
                // "dov'e'" perde credibilita' proprio dove doveva guadagnarla. Meglio rifiutare e
                // far correggere il periodo, che e' quasi sempre la vera intenzione.
                var overlapping = await _context.InitiativeSchedules
                    .AsNoTracking()
                    .Where(x => x.IdInitiative == id
                                && x.IdUser == schedule.IdUser
                                && x.Id != schedule.Id
                                && x.Start < schedule.End
                                && x.End > schedule.Start)
                    .OrderBy(x => x.Start)
                    .FirstOrDefaultAsync();

                if (overlapping != null)
                {
                    return FailSchedule(
                        $"Periodo sovrapposto a una presenza gia' registrata per questa persona "
                        + $"({overlapping.Start:dd/MM HH:mm} - {overlapping.End:dd/MM HH:mm}).",
                        System.Net.HttpStatusCode.Conflict);
                }

                InitiativeSchedule item;
                if (schedule.Id > 0)
                {
                    item = await _context.InitiativeSchedules.FirstOrDefaultAsync(x => x.Id == schedule.Id && x.IdInitiative == id)
                        ?? throw new InvalidOperationException("Presenza non trovata");
                }
                else
                {
                    item = new InitiativeSchedule
                    {
                        IdInitiative = id,
                        CreatedAt = DateTime.Now,
                        CreatedBy = await SafeCurrentUserAsync()
                    };
                    _context.InitiativeSchedules.Add(item);
                }

                item.IdUser = schedule.IdUser;
                item.Start = schedule.Start;
                item.End = schedule.End;
                item.Type = schedule.Type;
                item.Location = schedule.Location;
                item.Notes = schedule.Notes;

                await EnsureMemberAsync(id, schedule.IdUser);
                await _context.SaveChangesAsync();

                var saved = (await GetSchedulesAsync(id)).First(x => x.Id == item.Id);
                return new APIResponseMessage<InitiativeScheduleDTO>
                {
                    State = true,
                    Data = saved,
                    Message = "Presenza salvata",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(SaveScheduleAsync), EventsTypes.Error, ex);
                return FailSchedule("Errore nel salvataggio della presenza", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> DeleteScheduleAsync(int id, int idSchedule)
        {
            try
            {
                var item = await _context.InitiativeSchedules.FirstOrDefaultAsync(x => x.Id == idSchedule && x.IdInitiative == id);
                if (item == null)
                    return false;

                _context.InitiativeSchedules.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(DeleteScheduleAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var item = await _context.Initiatives.FindAsync(id);
                if (item == null)
                    return false;

                // Sgancio prima della cancellazione: l'iniziativa sparisce, cio' che ha prodotto no.
                // Le FK sono NO ACTION apposta, quindi senza questo passaggio la DELETE fallirebbe;
                // ed e' la scelta giusta, perche' l'alternativa - la cascata - porterebbe via lead,
                // opportunita' e note spese senza che nessuno se ne accorga.
                await _context.Activities.Where(x => x.IdInitiative == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IdInitiative, (int?)null));
                await _context.ExpenseReceipts.Where(x => x.IdInitiative == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IdInitiative, (int?)null));
                await _context.Leads.Where(x => x.IdInitiative == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IdInitiative, (int?)null));
                await _context.Deals.Where(x => x.IdInitiative == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IdInitiative, (int?)null));

                _context.Initiatives.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        // ------------------------------------------------------------------------------------
        // Resoconto
        // ------------------------------------------------------------------------------------

        public async Task<InitiativeSummaryDTO?> GetReportAsync(int id)
        {
            try
            {
                var initiative = await _context.Initiatives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (initiative == null)
                    return null;

                var report = new InitiativeSummaryDTO
                {
                    Id = initiative.Id,
                    Name = initiative.Name,
                    Kind = initiative.Kind,
                    State = initiative.State,
                    DateFrom = initiative.DateFrom,
                    DateTo = initiative.DateTo,
                    BudgetPlanned = initiative.BudgetPlanned
                };

                await FillCostsAsync(report, id);
                var activityIds = await FillActivitiesAsync(report, id);
                await FillDocumentsAsync(report, initiative, activityIds);
                FillReturnIndicators(report, initiative.Kind);

                return report;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesService), nameof(GetReportAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        private async Task FillCostsAsync(InitiativeSummaryDTO report, int id)
        {
            var expenses = await _context.ExpenseReceipts
                .AsNoTracking()
                .Where(x => x.IdInitiative == id)
                .Select(x => new
                {
                    x.IdUserSpender,
                    SpenderName = x.UserSpender != null ? x.UserSpender.NameComplete : string.Empty,
                    x.AmountBase
                })
                .ToListAsync();

            report.ExpenseCount = expenses.Count;

            // Le spese senza importo in valuta base non entrano nel totale e non spariscono: si
            // contano a parte. Un consuntivo che tace una spesa vale meno di un consuntivo assente.
            report.ExpensePendingConversion = expenses.Count(x => x.AmountBase == null);
            report.CostTotal = expenses.Where(x => x.AmountBase != null).Sum(x => x.AmountBase!.Value);

            report.CostByUser = expenses
                .GroupBy(x => new { x.IdUserSpender, x.SpenderName })
                .Select(g => new InitiativeCostByUserDTO
                {
                    IdUser = g.Key.IdUserSpender ?? string.Empty,
                    UserName = string.IsNullOrWhiteSpace(g.Key.SpenderName) ? "(non indicato)" : g.Key.SpenderName,
                    Amount = g.Where(x => x.AmountBase != null).Sum(x => x.AmountBase!.Value),
                    Count = g.Count(),
                    PendingConversion = g.Count(x => x.AmountBase == null)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();
        }

        /// <summary>Riempie la parte "cosa e' successo" e restituisce gli id delle attivita' agganciate.</summary>
        private async Task<List<int>> FillActivitiesAsync(InitiativeSummaryDTO report, int id)
        {
            var activities = await _context.Activities
                .AsNoTracking()
                .Where(a => a.IdInitiative == id)
                .Select(a => new
                {
                    a.Id,
                    a.Kind,
                    a.Subject,
                    a.EntityType,
                    a.EntityId,
                    a.State,
                    a.DueDate,
                    a.DoneDate,
                    a.Outcome,
                    UserName = a.User != null ? a.User.NameComplete : string.Empty
                })
                .ToListAsync();

            report.ActivityTotal = activities.Count;
            report.ActivityDone = activities.Count(a => a.State == ActivityState.Done);
            report.ActivityPlanned = activities.Count(a => a.State == ActivityState.Planned);

            var names = await ResolveEntityNamesAsync(activities
                .Select(a => (a.EntityType, a.EntityId))
                .Distinct()
                .ToList());

            report.Visits = activities
                .Select(a => new InitiativeVisitDTO
                {
                    IdActivity = a.Id,
                    Kind = a.Kind,
                    Subject = a.Subject,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    EntityName = names.TryGetValue((a.EntityType, a.EntityId), out var n) ? n : string.Empty,
                    Date = a.DoneDate ?? a.DueDate,
                    State = a.State,
                    Outcome = a.Outcome,
                    UserName = a.UserName
                })
                .OrderBy(v => v.Date ?? DateTime.MaxValue)
                .ToList();

            return activities.Select(a => a.Id).ToList();
        }

        /// <summary>
        /// Nome dell'entita' visitata, risolto a lotti per tipo. Il legame dell'attivita' e'
        /// polimorfico e non ha una FK: senza questa risoluzione il resoconto elencherebbe numeri.
        /// </summary>
        private async Task<Dictionary<(ActivityEntityType, int), string>> ResolveEntityNamesAsync(
            List<(ActivityEntityType Type, int Id)> refs)
        {
            var result = new Dictionary<(ActivityEntityType, int), string>();
            if (refs.Count == 0)
                return result;

            var byType = refs.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().ToList());

            if (byType.TryGetValue(ActivityEntityType.Company, out var companyIds))
            {
                foreach (var x in await _context.Companies.AsNoTracking()
                             .Where(c => companyIds.Contains(c.Id))
                             .Select(c => new { c.Id, c.RagioneSociale }).ToListAsync())
                    result[(ActivityEntityType.Company, x.Id)] = x.RagioneSociale;
            }

            if (byType.TryGetValue(ActivityEntityType.Contact, out var contactIds))
            {
                foreach (var x in await _context.Contacts.AsNoTracking()
                             .Where(c => contactIds.Contains(c.Id))
                             .Select(c => new { c.Id, c.Name, c.Surname }).ToListAsync())
                    result[(ActivityEntityType.Contact, x.Id)] = $"{x.Surname} {x.Name}".Trim();
            }

            if (byType.TryGetValue(ActivityEntityType.Lead, out var leadIds))
            {
                foreach (var x in await _context.Leads.AsNoTracking()
                             .Where(c => leadIds.Contains(c.Id))
                             .Select(c => new { c.Id, c.Name, c.CompanyName }).ToListAsync())
                    result[(ActivityEntityType.Lead, x.Id)] =
                        string.IsNullOrWhiteSpace(x.CompanyName) ? x.Name : $"{x.Name} ({x.CompanyName})";
            }

            if (byType.TryGetValue(ActivityEntityType.Deal, out var dealIds))
            {
                foreach (var x in await _context.Deals.AsNoTracking()
                             .Where(c => dealIds.Contains(c.Id))
                             .Select(c => new { c.Id, c.Name }).ToListAsync())
                    result[(ActivityEntityType.Deal, x.Id)] = x.Name;
            }

            if (byType.TryGetValue(ActivityEntityType.Ticket, out var ticketIds))
            {
                foreach (var idTicket in ticketIds)
                    result[(ActivityEntityType.Ticket, idTicket)] = $"Ticket #{idTicket}";
            }

            return result;
        }

        /// <summary>
        /// Opportunita', preventivi e ordini prodotti. La strada verso le opportunita' cambia col
        /// tipo di iniziativa, e non per comodita':
        /// <list type="bullet">
        /// <item>fiera: attribuzione DIRETTA, perche' meta' delle opportunita' nascono settimane
        /// dopo da un biglietto da visita e non hanno nessuna attivita' di origine;</item>
        /// <item>trasferta: si passa dall'attivita', perche' li' l'attivita' c'e' sempre e
        /// l'opportunita' appartiene al cliente, non al viaggio.</item>
        /// </list>
        /// </summary>
        private async Task FillDocumentsAsync(InitiativeSummaryDTO report, Initiative initiative, List<int> activityIds)
        {
            var isOriginating = initiative.Kind.HasMeaningfulRoi();

            if (isOriginating)
            {
                var leads = await _context.Leads.AsNoTracking()
                    .Where(l => l.IdInitiative == initiative.Id)
                    .Select(l => new { l.Status })
                    .ToListAsync();

                report.LeadTotal = leads.Count;
                report.LeadConverted = leads.Count(l => l.Status == LeadStatus.Converted);
                report.LeadsByStatus = leads
                    .GroupBy(l => l.Status)
                    .Select(g => new InitiativeLeadStatusDTO { Status = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Status)
                    .ToList();
            }

            var dealsQuery = isOriginating
                ? _context.Deals.Where(d => d.IdInitiative == initiative.Id)
                : _context.Deals.Where(d => d.IdActivityOrigin != null && activityIds.Contains(d.IdActivityOrigin.Value));

            var deals = await dealsQuery
                .AsNoTracking()
                .Select(d => new { d.Id, d.Name, d.Amount, d.Date, d.State })
                .ToListAsync();

            report.DealCount = deals.Count;
            report.DealAmount = deals.Sum(d => d.Amount);
            report.DealWonCount = deals.Count(d => d.State == DealStates.CloseWon);
            report.DealWonAmount = deals.Where(d => d.State == DealStates.CloseWon).Sum(d => d.Amount);
            report.Deals = deals
                .OrderByDescending(d => d.Date)
                .Select(d => new ActivityGeneratedItemDTO
                {
                    Id = d.Id,
                    Label = d.Name,
                    Amount = d.Amount,
                    Date = d.Date,
                    State = d.State.ToString()
                })
                .ToList();

            // I preventivi si raggiungono dall'attivita' di origine e, in fiera, anche
            // dall'opportunita' attribuita: sono le due strade da cui un preventivo puo' nascere.
            var dealIds = deals.Select(d => d.Id).ToList();
            var quotes = await _context.Quotes
                .AsNoTracking()
                .Where(q => (q.IdActivityOrigin != null && activityIds.Contains(q.IdActivityOrigin.Value))
                            || (q.IdDeal != null && dealIds.Contains(q.IdDeal.Value)))
                .Select(q => new { q.Id, q.Number, q.Revision, q.Total, q.Date, q.State })
                .ToListAsync();

            report.QuoteCount = quotes.Count;
            report.QuoteAmount = quotes.Sum(q => q.Total);
            report.Quotes = quotes
                .OrderByDescending(q => q.Date)
                .Select(q => new ActivityGeneratedItemDTO
                {
                    Id = q.Id,
                    Label = string.IsNullOrWhiteSpace(q.Number) ? $"Preventivo #{q.Id}" : $"{q.Number} rev.{q.Revision}",
                    Amount = q.Total,
                    Date = q.Date,
                    State = q.State.ToString()
                })
                .ToList();

            var quoteIds = quotes.Select(q => q.Id).ToList();
            if (quoteIds.Count == 0)
                return;

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.IdQuote != null && quoteIds.Contains(o.IdQuote.Value))
                .Select(o => new { o.Id, o.Number, o.Total, o.Date, o.State })
                .ToListAsync();

            report.OrderCount = orders.Count;
            report.OrderAmount = orders.Sum(o => o.Total);
            report.Orders = orders
                .OrderByDescending(o => o.Date)
                .Select(o => new ActivityGeneratedItemDTO
                {
                    Id = o.Id,
                    Label = string.IsNullOrWhiteSpace(o.Number) ? $"Ordine #{o.Id}" : o.Number!,
                    Amount = o.Total,
                    Date = o.Date,
                    State = o.State.ToString()
                })
                .ToList();
        }

        /// <summary>
        /// Costo per lead e ROI, ma solo dove sono domande sensate. Sulle trasferte restano null:
        /// i clienti c'erano gia' e l'ordine firmato in visita sarebbe probabilmente arrivato lo
        /// stesso, quindi un ritorno calcolato sarebbe un numero preciso e falso. La regola sta qui
        /// e non nell'interfaccia, cosi' non puo' essere aggirata da una pagina distratta.
        /// </summary>
        private static void FillReturnIndicators(InitiativeSummaryDTO report, InitiativeKind kind)
        {
            if (!kind.HasMeaningfulRoi())
                return;

            if (report.LeadTotal > 0)
                report.CostPerLead = Math.Round(report.CostTotal / report.LeadTotal, 2);

            if (report.CostTotal > 0)
                report.Roi = Math.Round((report.DealWonAmount - report.CostTotal) / report.CostTotal, 4);
        }

        // ------------------------------------------------------------------------------------
        // Interrogazioni di base
        // ------------------------------------------------------------------------------------

        private IQueryable<Initiative> FilterItems(InitiativeFilter? args)
        {
            var q = BaseQuery();

            if (args?.Kind != null)
                q = q.Where(x => x.Kind == args.Kind);

            if (args?.State != null)
                q = q.Where(x => x.State == args.State);

            if (!string.IsNullOrWhiteSpace(args?.IdOwner))
                q = q.Where(x => x.IdOwner == args.IdOwner);

            // Periodo: si prendono le iniziative che si SOVRAPPONGONO all'intervallo, non quelle
            // interamente contenute. Un giro di dieci giorni a cavallo di due mesi deve comparire
            // in entrambi, altrimenti sparisce da tutti e due.
            if (args?.DateFrom != null)
                q = q.Where(x => x.DateTo >= args.DateFrom);

            if (args?.DateTo != null)
                q = q.Where(x => x.DateFrom <= args.DateTo);

            if (!string.IsNullOrWhiteSpace(args?.Search))
            {
                var search = args.Search.Trim();
                q = q.Where(x =>
                    x.Name.Contains(search) ||
                    (x.Location != null && x.Location.Contains(search)) ||
                    (x.Objective != null && x.Objective.Contains(search)));
            }

            return GridSort.Apply(q, args?.OrderBy, SortableColumns,
                items => items.OrderByDescending(x => x.DateFrom).ThenBy(x => x.Name));
        }

        private IQueryable<Initiative> BaseQuery()
            => _context.Initiatives
                .Include(x => x.Owner)
                .Include(x => x.Members)
                    .ThenInclude(p => p.User)
                .Include(x => x.Schedules)
                    .ThenInclude(p => p.User);

        private async Task<List<InitiativeDTO>> ToDtosAsync(IQueryable<Initiative> q)
        {
            var currentUser = await SafeCurrentUserAsync();
            var items = await q.AsNoTracking().ToListAsync();

            var dtos = items.Select(x =>
            {
                var dto = x.ToDTO()!;
                dto.Permits = ComputePermits(x.IdOwner, currentUser);
                return dto;
            }).ToList();

            await FillCountersAsync(dtos);
            return dtos;
        }

        /// <summary>
        /// Costo e numero di attivita' per l'elenco, in due query sole invece che due per riga.
        /// </summary>
        private async Task FillCountersAsync(List<InitiativeDTO> dtos)
        {
            if (dtos.Count == 0)
                return;

            var ids = dtos.Select(d => d.Id).ToList();

            var costs = await _context.ExpenseReceipts
                .AsNoTracking()
                .Where(x => x.IdInitiative != null && ids.Contains(x.IdInitiative.Value) && x.AmountBase != null)
                .GroupBy(x => x.IdInitiative!.Value)
                .Select(g => new { Id = g.Key, Amount = g.Sum(x => x.AmountBase!.Value) })
                .ToDictionaryAsync(x => x.Id, x => x.Amount);

            var activities = await _context.Activities
                .AsNoTracking()
                .Where(x => x.IdInitiative != null && ids.Contains(x.IdInitiative.Value))
                .GroupBy(x => x.IdInitiative!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            foreach (var dto in dtos)
            {
                dto.CostTotal = costs.TryGetValue(dto.Id, out var c) ? c : 0;
                dto.ActivityCount = activities.TryGetValue(dto.Id, out var a) ? a : 0;
            }
        }

        /// <summary>
        /// Allinea i membri a quelli richiesti, riconciliando invece di cancellare e ricreare.
        /// <para>
        /// La differenza non e' di stile: svuotando la raccolta si perdevano <c>AddedAt</c> e
        /// <c>AddedBy</c> di chi c'era gia', e ogni salvataggio dell'anagrafica riscriveva il ruolo
        /// col valore di default della maschera - un "Tecnico" tornava "Partecipante" senza che
        /// nessuno avesse chiesto niente e senza lasciare traccia.
        /// </para>
        /// <para>
        /// Chi viene tolto si porta via le proprie presenze: una persona che non fa piu' parte
        /// dell'iniziativa non puo' esserci stata, e lasciarle la mostrerebbe ancora occupata in
        /// agenda. Restituisce gli id dei membri NUOVI, che sono quelli a cui va data una presenza
        /// di default.
        /// </para>
        /// </summary>
        private async Task<List<string>> ReconcileMembersAsync(Initiative initiative, IEnumerable<InitiativeMember> rows)
        {
            var wanted = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.IdUser))
                .GroupBy(r => r.IdUser)
                .Select(g => g.First())
                .ToList();

            var wantedIds = wanted.Select(r => r.IdUser).ToHashSet(StringComparer.Ordinal);
            var removed = initiative.Members.Where(m => !wantedIds.Contains(m.IdUser)).ToList();

            foreach (var member in removed)
                initiative.Members.Remove(member);

            if (removed.Count > 0 && initiative.Id > 0)
            {
                var removedIds = removed.Select(m => m.IdUser).ToList();
                var orphanSchedules = await _context.InitiativeSchedules
                    .Where(s => s.IdInitiative == initiative.Id && removedIds.Contains(s.IdUser))
                    .ToListAsync();

                _context.InitiativeSchedules.RemoveRange(orphanSchedules);
            }

            var currentUser = await SafeCurrentUserAsync();
            var added = new List<string>();

            foreach (var row in wanted)
            {
                var member = initiative.Members.FirstOrDefault(m => m.IdUser == row.IdUser);
                if (member == null)
                {
                    initiative.Members.Add(new InitiativeMember
                    {
                        IdUser = row.IdUser,
                        Role = row.Role,
                        Notes = row.Notes,
                        AddedAt = DateTime.Now,
                        AddedBy = currentUser
                    });

                    added.Add(row.IdUser);
                    continue;
                }

                // Chi c'era gia' conserva quando e da chi e' stato aggiunto: e' la sola traccia di
                // come si e' composta la squadra, e non la riscrive un salvataggio dell'anagrafica.
                member.Role = row.Role;
                member.Notes = row.Notes;
            }

            return added;
        }

        /// <summary>
        /// Da a ogni nuovo membro una presenza sull'intero periodo dell'iniziativa.
        /// <para>
        /// E' il pezzo che rende vero l'elenco. Senza, chi compila la maschera aggiunge cinque
        /// nomi da una tendina e ha finito - nessuno andra' poi a creare cinque presenze a mano -
        /// e l'agenda resta vuota: alla domanda "dov'e' oggi" il sistema continua a rispondere
        /// "libero" mentre la persona e' in fiera. La presenza di default e' un'ipotesi
        /// ragionevole e visibile, che chi ruota accorcia e chi non c'e' cancella.
        /// </para>
        /// <para>
        /// Non tocca chi ha gia' una presenza: riaggiungere un membro non deve far ricomparire un
        /// periodo che qualcuno aveva deliberatamente ridotto.
        /// </para>
        /// </summary>
        private async Task CreateDefaultSchedulesAsync(Initiative initiative, IReadOnlyCollection<string> userIds)
        {
            if (userIds.Count == 0)
                return;

            var alreadyScheduled = await _context.InitiativeSchedules
                .Where(s => s.IdInitiative == initiative.Id && userIds.Contains(s.IdUser))
                .Select(s => s.IdUser)
                .Distinct()
                .ToListAsync();

            var currentUser = await SafeCurrentUserAsync();

            // Fine a 23:59:59 dell'ultimo giorno: l'iniziativa dura giornate intere, e chiudere a
            // mezzanotte precisa lascerebbe fuori l'ultimo giorno da ogni confronto sul periodo.
            var start = initiative.DateFrom.Date;
            var end = initiative.DateTo.Date.AddDays(1).AddSeconds(-1);

            foreach (var idUser in userIds.Where(id => !alreadyScheduled.Contains(id)))
            {
                _context.InitiativeSchedules.Add(new InitiativeSchedule
                {
                    IdInitiative = initiative.Id,
                    IdUser = idUser,
                    Start = start,
                    End = end,
                    Type = InitiativeScheduleType.Presence,
                    Location = initiative.Location,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser
                });
            }
        }

        private async Task EnsureMemberAsync(int idInitiative, string idUser)
        {
            if (await _context.InitiativeMembers.AnyAsync(x => x.IdInitiative == idInitiative && x.IdUser == idUser))
                return;

            _context.InitiativeMembers.Add(new InitiativeMember
            {
                IdInitiative = idInitiative,
                IdUser = idUser,
                Role = InitiativeMemberRole.Participant,
                AddedAt = DateTime.Now,
                AddedBy = await SafeCurrentUserAsync()
            });
        }

        private async Task<string?> SafeCurrentUserAsync()
        {
            try { return await _permitsService.IdUser(); }
            catch { return null; }
        }

        private static int ComputePermits(string? ownerId, string? currentUserId)
        {
            var permits = PermitsHelper.SetRead(0);
            permits = PermitsHelper.SetInsert(permits);
            if (string.IsNullOrEmpty(currentUserId) || ownerId == currentUserId || string.IsNullOrEmpty(ownerId))
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetDelete(permits);
            }

            return permits;
        }

        private static APIResponseMessage<InitiativeDTO> Fail(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static APIResponseMessage<InitiativeDTO> Ok(InitiativeDTO data, string msg)
            => new() { State = true, Data = data, Message = msg, Code = System.Net.HttpStatusCode.OK };

        private static APIResponseMessage<InitiativeScheduleDTO> FailSchedule(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };
    }
}
