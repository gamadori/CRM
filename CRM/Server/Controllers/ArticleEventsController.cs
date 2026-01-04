using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace CRM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticleEventsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public ArticleEventsController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpPost]
    public async Task<ActionResult<ArticleEvent>> CreateEvent([FromBody] CreateArticleEventDto dto)
    {
        // Recupera stato corrente
        var currentDomainState = await _context.ArticleDomainStates
            .Include(ads => ads.CurrentState)
            .FirstOrDefaultAsync(ads => ads.ArticleId == dto.ArticleId && ads.DomainId == dto.DomainId);
        
        var eventType = await _context.ArticleEventTypes.FindAsync(dto.EventTypeId);
        
        // Determina lo stato di destinazione
        int? toStateId = eventType.SetsStateId ?? await DetermineNextState(dto.EventTypeId, currentDomainState?.CurrentStateId);
        
        var articleEvent = new ArticleEvent
        {
            ArticleId = dto.ArticleId,
            DomainId = dto.DomainId,
            EventTypeId = dto.EventTypeId,
            FromStateId = currentDomainState?.CurrentStateId,
            ToStateId = toStateId,
            OccurredAt = DateTime.UtcNow,
            Note = dto.Note,
            ActorUserId = User.Identity.Name,
            NewOwnerId = dto.NewOwnerId
        };
        
        _context.ArticleEvents.Add(articleEvent);
        await _context.SaveChangesAsync();
        
        // Aggiorna ArticleDomainState
        if (currentDomainState != null && toStateId.HasValue)
        {
            currentDomainState.CurrentStateId = toStateId.Value;
            currentDomainState.LastEventId = articleEvent.Id;
            currentDomainState.UpdatedAt = DateTime.UtcNow;
        }
        else if (toStateId.HasValue)
        {
            _context.ArticleDomainStates.Add(new ArticleDomainState
            {
                ArticleId = dto.ArticleId,
                DomainId = dto.DomainId,
                CurrentStateId = toStateId.Value,
                LastEventId = articleEvent.Id,
                UpdatedAt = DateTime.UtcNow
            });
        }
        
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetEvent), new { id = articleEvent.Id }, articleEvent);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<ArticleEvent>> GetEvent(int id)
    {
        var evt = await _context.ArticleEvents
            .Include(e => e.Domain)
            .Include(e => e.EventType)
            .Include(e => e.FromState)
            .Include(e => e.ToState)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (evt == null) return NotFound();
        return evt;
    }
    
    private async Task<int?> DetermineNextState(int eventTypeId, int? currentStateId)
    {
        if (!currentStateId.HasValue) return null;
        
        var transition = await _context.ArticleStateTransitions
            .FirstOrDefaultAsync(t => t.EventTypeId == eventTypeId && t.FromStateId == currentStateId.Value);
        
        return transition?.ToStateId;
    }
}

public class CreateArticleEventDto
{
    public int ArticleId { get; set; }
    public int DomainId { get; set; }
    public int EventTypeId { get; set; }
    public string Note { get; set; }
    public int? NewOwnerId { get; set; }
}

