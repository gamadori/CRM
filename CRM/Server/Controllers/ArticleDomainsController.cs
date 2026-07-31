using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticleDomainsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public ArticleDomainsController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpGet]
    public async Task<ActionResult<List<ArticleDomain>>> GetDomains()
    {
        return await _context.ArticleDomains
            .OrderBy(d => d.SortedOrder)
            .ToListAsync();
    }
    
    [HttpGet("{id}/AvailableEvents")]
    public async Task<ActionResult<List<ArticleEventType>>> GetAvailableEvents(
        int id, 
        [FromQuery] int? currentStateId)
    {
        // Se c'è uno stato corrente, filtra gli eventi disponibili in base alle transizioni
        if (currentStateId.HasValue)
        {
            var availableEventIds = await _context.ArticleStateTransitions
                .Where(t => t.DomainId == id && t.FromStateId == currentStateId.Value)
                .Select(t => t.EventTypeId)
                .Distinct()
                .ToListAsync();
            
            return await _context.ArticleEventTypes
                .Where(et => et.DomainId == id && availableEventIds.Contains(et.Id))
                .ToListAsync();
        }
        
        // Altrimenti restituisci tutti gli eventi del dominio
        return await _context.ArticleEventTypes
            .Where(et => et.DomainId == id)
            .ToListAsync();
    }
}