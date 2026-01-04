using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;


namespace CRM.Server.Services;

public class ArticleEventService: IArticleEventService
{
    private readonly ApplicationDbContext _context;
    public ArticleEventService(ApplicationDbContext context) => _context = context;

    public async Task<int> AddEventAsync(int itemId, AddItemEventRequest req, string? actorUserId = null)
    {
        // carico stato corrente del dominio con tracking (serve concorrenza)
        var ids = await _context.ArticleDomainStates
            .SingleOrDefaultAsync(x => x.Id == itemId && x.DomainId == req.DomainId);

        if (ids is null)
            throw new InvalidOperationException("ItemDomainState mancante (inizializza gli stati per tutti i domini).");

        // check rowversion (concorrenza)
        if (!ids.RowVer.SequenceEqual(req.ItemDomainStateRowVer))
            throw new DbUpdateConcurrencyException("Conflitto: lo stato è cambiato (qualcun altro ha inserito un evento).");

        // carico EventType
        var evType = await _context.ArticleEventTypes.SingleAsync(x => x.Id == req.EventTypeId && x.DomainId == req.DomainId);

        if (evType.RequiresOwner && req.NewOwnerId is null)
            throw new InvalidOperationException("Questo evento richiede NewOwnerId.");

        // calcolo transizione ammessa
        var currentStateId = ids.CurrentStateId;

        var transition = await _context.ArticleStateTransitions
            .SingleOrDefaultAsync(t => t.DomainId == req.DomainId
                                   && t.FromStateId == currentStateId
                                   && t.EventTypeId == req.EventTypeId);

        if (transition is null)
            throw new InvalidOperationException("Transizione non ammessa per lo stato corrente.");

        var toStateId = transition.ToStateId;

        // append-only: creo evento e aggiorno stato corrente del dominio
        using var tx = await _context.Database.BeginTransactionAsync();

        var ev = new ArticleEvent
        {
            Id = itemId,
            DomainId = req.DomainId,
            EventTypeId = req.EventTypeId,
            FromStateId = currentStateId,
            ToStateId = toStateId,
            OccurredAt = req.OccurredAt ?? DateTime.UtcNow,
            Note = req.Note,
            ActorUserId = actorUserId,
            NewOwnerId = req.NewOwnerId
        };

        _context.ArticleEvents.Add(ev);
        await _context.SaveChangesAsync();

        ids.CurrentStateId = toStateId;
        ids.LastEventId = ev.Id;
        ids.UpdatedAt = DateTime.UtcNow;

        // se evento comporta cambio proprietario -> aggiorno Item.ClienteAttualeId SOLO qui
        if (req.NewOwnerId is not null && (evType.Code == "SELL" || evType.Code == "TRANSFER"))
        {
            var item = await _context.Articles.SingleAsync(i => i.Id == itemId);
            item.IdCompany = req.NewOwnerId;
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return ev.Id;
    }

    public async Task<AvailableEventsResponse> GetAvailableEventsAsync(int itemId, int domainId)
    {
        var dom = await _context.ArticleDomains.SingleAsync(d => d.Id == domainId);

        var ids = await _context.ArticleDomainStates
            .AsNoTracking()
            .SingleAsync(x => x.Id == itemId && x.DomainId == domainId);

        var curState = await _context.ArticleStates.SingleAsync(s => s.Id == ids.CurrentStateId);

        var available = await _context.ArticleStateTransitions
            .Where(t => t.DomainId == domainId && t.FromStateId == ids.CurrentStateId)
            .Join(_context.ArticleEventTypes, t => t.EventTypeId, e => e.Id, (t, e) => e)
            .OrderBy(e => e.Name)
            .Select(e => new EventTypeDto(e.Id, e.DomainId, e.Code, e.Name, e.RequiresOwner))
            .ToListAsync();

        return new AvailableEventsResponse(domainId, dom.Code, curState.Id, curState.Code, available);
    }

    public async Task<List<ArticleEventDto>> Timeline(int itemId)
    {
        var q = from ev in _context.ArticleEvents.AsNoTracking()
                join d in _context.ArticleDomains.AsNoTracking() on ev.DomainId equals d.Id
                join et in _context.ArticleEventTypes.AsNoTracking() on ev.EventTypeId equals et.Id
                join fs in _context.ArticleStates.AsNoTracking() on ev.FromStateId equals fs.Id into fsg
                from fs in fsg.DefaultIfEmpty()
                join ts in _context.ArticleStates.AsNoTracking() on ev.ToStateId equals ts.Id into tsg
                from ts in tsg.DefaultIfEmpty()
                where ev.ArticleId == itemId
                orderby ev.OccurredAt descending, ev.ArticleId descending
                select new ArticleEventDto(
                    ev.ArticleId, ev.Id,
                    ev.DomainId, d.Code,
                    ev.EventTypeId, et.Code, et.Name,
                    ev.FromStateId, fs != null ? fs.Code : null,
                    ev.ToStateId, ts != null ? ts.Code : null,
                    ev.OccurredAt, ev.Note, ev.NewOwnerId
                );

        return await q.ToListAsync();
    }

    public async Task<List<DomainStateDto>> DomainStates(int itemId)
    {
        var rows = await _context.ArticleDomainStates.AsNoTracking()
            .Where(x => x.Id == itemId)
            .Join(_context.ArticleDomains, x => x.DomainId, d => d.Id, (x, d) => new { x, d })
            .Join(_context.ArticleStates, xd => xd.x.CurrentStateId, s => s.Id, (xd, s) => new DomainStateDto(
            
                xd.x.Id,
                xd.x.DomainId,
                xd.d.Code,
                s.Id,
                s.Code,
                xd.x.RowVer
            ))
            .ToListAsync();

        return rows;
    }
}
