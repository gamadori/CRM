using CRM.Server.Data;
using CRM.Server.Models;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
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
    [Route("api/[controller]")]
    [ApiController]
    public class TicketInterventionUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITicketsService _ticketsService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TicketInterventionUsersController(
            ApplicationDbContext context,
            ITicketsService ticketsService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _ticketsService = ticketsService;
            _userManager = userManager;
        }

        // GET: api/TicketInterventionUsers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketInterventionUser>>> GetTicketInterventionUser()
        {
            return await _context.TicketInterventionUser.ToListAsync();
        }

        // GET: api/TicketInterventionUsers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketInterventionUser>> GetTicketInterventionUser(int id)
        {
            var ticketInterventionUser = await _context.TicketInterventionUser.FindAsync(id);

            if (ticketInterventionUser == null)
            {
                return NotFound();
            }

            return ticketInterventionUser;
        }

        // PUT: api/TicketInterventionUsers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketInterventionUser(int id, TicketInterventionUser ticketInterventionUser)
        {
            if (id != ticketInterventionUser.Id)
            {
                return BadRequest();
            }

            _context.Entry(ticketInterventionUser).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketInterventionUserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

      
        // POST: api/TicketInterventionUsers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TicketInterventionUser>> PostTicketInterventionUser(TicketInterventionUser ticketInterventionUser)
        {
            var user = await _context.Users.Where(x => x.Id == ticketInterventionUser.IdUser).SingleOrDefaultAsync();
            var intervention = await _context.TicketsInterventions.Where(x => x.Id == ticketInterventionUser.IdIntervention).SingleOrDefaultAsync();

            if (user == null || intervention == null)
                return NotFound();

            _context.TicketInterventionUser.Add(ticketInterventionUser);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTicketInterventionUser", new { id = ticketInterventionUser.Id }, ticketInterventionUser);
        }


        // DELETE: api/TicketInterventionUsers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketInterventionUser(int id)
        {
            var ticketInterventionUser = await _context.TicketInterventionUser.FindAsync(id);
            if (ticketInterventionUser == null)
            {
                return NotFound();
            }

            _context.TicketInterventionUser.Remove(ticketInterventionUser);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ✅ NUOVO: GET /api/TicketInterventionUsers/intervention/{id}/assigned-users
        // Recupera gli utenti assegnati a un intervento specifico
        [HttpGet("intervention/{id}/assigned-users")]
        public async Task<ActionResult<List<string>>> GetInterventionAssignedUsers(int id)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(i => i.AssignedUsers)
                        .ThenInclude(au => au.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (intervention == null)
                {
                    return NotFound($"Intervention con ID {id} non trovato");
                }

                // Restituisce la lista degli ID utenti assegnati
                var userIds = intervention.AssignedUsers
                    .Select(au => au.IdUser)
                    .ToList();

                return Ok(userIds);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        
        // ✅ NUOVO: POST /api/TicketInterventionUsers/intervention/{id}/assign-users
        // Assegna multipli utenti a un intervento (sostituisce assegnazioni esistenti).
        //
        // Registrare un intervento E' la presa in carico: chi non ha ancora un'assegnazione
        // individuale sul ticket la ottiene qui, purche' sia legittimato (gruppo assegnato o
        // utenti abilitati sul tipo di ticket). Prima l'endpoint rifiutava questi utenti,
        // e un ticket smistato solo a un gruppo non permetteva di registrare nulla.
        [HttpPost("intervention/{id}/assign-users")]
        public async Task<IActionResult> AssignUsersToIntervention(int id, [FromBody] AssignUsersRequest assignUsers)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(i => i.AssignedUsers)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (intervention == null)
                {
                    return NotFound($"Intervention con ID {id} non trovato");
                }

                // Un intervento senza persone non e' registrabile: ore, rapportini e fatturazione
                // sono atti di individui, non di gruppi.
                if (assignUsers.UserIds == null || !assignUsers.UserIds.Any())
                {
                    return BadRequest("Almeno un utente deve essere assegnato all'intervento");
                }

                var currentUserId = (await _userManager.GetUserAsync(User))?.Id;

                var claim = await _ticketsService.EnsureUsersAssignedAsync(
                    intervention.IdTicket,
                    assignUsers.UserIds,
                    currentUserId);

                if (!claim.Success)
                {
                    return claim.Error switch
                    {
                        EnsureAssignedError.TicketNotFound => NotFound(claim.ErrorMessage),
                        EnsureAssignedError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, claim.ErrorMessage),
                        EnsureAssignedError.UserNotEligible => BadRequest(claim.ErrorMessage),
                        _ => StatusCode(StatusCodes.Status500InternalServerError, claim.ErrorMessage)
                    };
                }

                // Rimuovi tutte le assegnazioni esistenti
                _context.TicketInterventionUser.RemoveRange(intervention.AssignedUsers);

                // Aggiungi le nuove assegnazioni
                foreach (var userId in assignUsers.UserIds.Distinct())
                {
                    var assignment = new TicketInterventionUser
                    {
                        IdIntervention = id,
                        IdUser = userId
                    };

                    _context.TicketInterventionUser.Add(assignment);
                }

                await _context.SaveChangesAsync();

                return Ok(new {
                    message = "Utenti assegnati con successo all'intervento",
                    assignedCount = assignUsers.UserIds.Count,
                    claimedCount = claim.ClaimedUserIds.Count,
                    claimedUsers = claim.ClaimedUserNames
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        private bool TicketInterventionUserExists(int id)
        {
            return _context.TicketInterventionUser.Any(e => e.Id == id);
        }
    }

    /// <summary>
    /// ✅ NUOVO: DTO per assegnazione multipla utenti a un intervento
    /// </summary>
    public class AssignInterventionUsersRequest
    {
        public int InterventionId { get; set; }
        public List<string> UserIds { get; set; } = new List<string>();
    }
}
