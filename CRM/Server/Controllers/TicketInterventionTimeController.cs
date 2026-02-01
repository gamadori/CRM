using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
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
    public class TicketInterventionTimeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TicketInterventionTimeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/TicketInterventionTime/5
        /// <summary>
        /// Ottiene tutti i tempi di un intervento
        /// </summary>
        [HttpGet("{idIntervention}")]
        public async Task<ActionResult<List<TicketInterventionTime>>> GetByIntervention(int idIntervention)
        {
            var times = await _context.TicketInterventionTimes
                .AsNoTracking()
                .Where(t => t.IdTicketIntervention == idIntervention)
                .OrderBy(t => t.StartDateTime)
                .Select(t => new TicketInterventionTime
                {
                    Id = t.Id,
                    IdTicketIntervention = t.IdTicketIntervention,
                    StartDateTime = t.StartDateTime,
                    EndDateTime = t.EndDateTime,
                    TimeType = t.TimeType,
                    Notes = t.Notes,
                    IsBillable = t.IsBillable,
                    TravelKilometers = t.TravelKilometers
                })
                .ToListAsync();

            return Ok(times);
        }

        // GET: api/TicketInterventionTime/single/123
        /// <summary>
        /// Ottiene un singolo tempo per ID
        /// </summary>
        [HttpGet("single/{id}")]
        public async Task<ActionResult<TicketInterventionTime>> GetById(int id)
        {
            var time = await _context.TicketInterventionTimes
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new TicketInterventionTime
                {
                    Id = t.Id,
                    IdTicketIntervention = t.IdTicketIntervention,
                    StartDateTime = t.StartDateTime,
                    EndDateTime = t.EndDateTime,
                    TimeType = t.TimeType,
                    Notes = t.Notes,
                    IsBillable = t.IsBillable,
                    TravelKilometers = t.TravelKilometers
                })
                .FirstOrDefaultAsync();

            if (time == null)
            {
                return NotFound();
            }

            return Ok(time);
        }

        // POST: api/TicketInterventionTime
        /// <summary>
        /// Crea un nuovo periodo di tempo
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TicketInterventionTime>> Create(TicketInterventionTime time)
        {
            if (time.EndDateTime <= time.StartDateTime)
            {
                return BadRequest("La data di fine deve essere successiva alla data di inizio");
            }

            // Crea un nuovo oggetto senza navigation properties
            var newTime = new TicketInterventionTime
            {
                IdTicketIntervention = time.IdTicketIntervention,
                StartDateTime = time.StartDateTime,
                EndDateTime = time.EndDateTime,
                TimeType = time.TimeType,
                Notes = time.Notes,
                IsBillable = time.IsBillable,
                TravelKilometers = time.TravelKilometers
            };

            _context.TicketInterventionTimes.Add(newTime);
            await _context.SaveChangesAsync();

            // Aggiorna il totale minuti dell'intervento
            await UpdateInterventionTotalMinutes(newTime.IdTicketIntervention);

            // Restituisci solo i campi necessari
            var result = new TicketInterventionTime
            {
                Id = newTime.Id,
                IdTicketIntervention = newTime.IdTicketIntervention,
                StartDateTime = newTime.StartDateTime,
                EndDateTime = newTime.EndDateTime,
                TimeType = newTime.TimeType,
                Notes = newTime.Notes,
                IsBillable = newTime.IsBillable,
                TravelKilometers = newTime.TravelKilometers
            };

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // PUT: api/TicketInterventionTime/5
        /// <summary>
        /// Aggiorna un periodo di tempo esistente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, TicketInterventionTime time)
        {
            if (id != time.Id)
            {
                return BadRequest();
            }

            if (time.EndDateTime <= time.StartDateTime)
            {
                return BadRequest("La data di fine deve essere successiva alla data di inizio");
            }

            _context.Entry(time).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                
                // Aggiorna il totale minuti dell'intervento
                await UpdateInterventionTotalMinutes(time.IdTicketIntervention);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await TimeExists(id))
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

        // DELETE: api/TicketInterventionTime/5
        /// <summary>
        /// Elimina un periodo di tempo
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var time = await _context.TicketInterventionTimes.FindAsync(id);
            if (time == null)
            {
                return NotFound();
            }

            var interventionId = time.IdTicketIntervention;

            _context.TicketInterventionTimes.Remove(time);
            await _context.SaveChangesAsync();

            // Aggiorna il totale minuti dell'intervento
            await UpdateInterventionTotalMinutes(interventionId);

            return NoContent();
        }

        // GET: api/TicketInterventionTime/summary/5
        /// <summary>
        /// Ottiene il riepilogo tempi di un intervento
        /// </summary>
        [HttpGet("summary/{idIntervention}")]
        public async Task<ActionResult<object>> GetSummary(int idIntervention)
        {
            var times = await _context.TicketInterventionTimes
                .Where(t => t.IdTicketIntervention == idIntervention)
                .ToListAsync();

            var summary = new
            {
                TotalWorkMinutes = times.TotalWorkMinutes(),
                TotalTravelMinutes = times.TotalTravelMinutes(),
                TotalBillableMinutes = times.TotalBillableMinutes(),
                TotalKilometers = times.TotalTravelKilometers(),
                TotalMinutes = times.Sum(t => t.DurationMinutes),
                TimeEntries = times.Count
            };

            return Ok(summary);
        }

        /// <summary>
        /// Aggiorna il campo Minute del TicketIntervention con il totale dei tempi fatturabili
        /// </summary>
        private async Task UpdateInterventionTotalMinutes(int interventionId)
        {
            var intervention = await _context.TicketsInterventions.FindAsync(interventionId);
            if (intervention == null) return;

            var times = await _context.TicketInterventionTimes
                .Where(t => t.IdTicketIntervention == interventionId)
                .ToListAsync();

            // Calcola il totale dei minuti fatturabili
            intervention.Minute = times.TotalBillableMinutes();

            _context.Entry(intervention).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        private async Task<bool> TimeExists(int id)
        {
            return await _context.TicketInterventionTimes.AnyAsync(e => e.Id == id);
        }
    }
}
