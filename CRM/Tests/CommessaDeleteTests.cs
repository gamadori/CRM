using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

public class CommessaDeleteTests
{
    [Fact]
    public async Task DeleteAsync_EliminaAncheITicketDellaCommessa()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-commessa-delete-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new ApplicationDbContext(options);
        var permits = Substitute.For<IPermitsService>();
        permits.GetVisibleCompanyIds().Returns((List<int>?)null);

        var service = new CommesseService(db, permits, Substitute.For<ILogEventService>());

        db.Commesse.Add(new Commessa
        {
            Id = 1,
            Code = "CM-DEL-0001",
            IdCompany = 1,
            State = CommessaStates.Planned,
            StartDatePlanned = DateTime.Today,
            EndDatePlanned = DateTime.Today.AddDays(10),
            CreatedAt = DateTime.Now
        });
        db.CommessaFasi.Add(new CommessaFase
        {
            Id = 10,
            IdCommessa = 1,
            Name = "Fase",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(2)
        });

        db.Tickets.AddRange(
            new Ticket
            {
                Id = 100,
                IdCommessaFase = 10,
                IdCompany = 1,
                IdUserOpened = ProductionTestContext.Utente,
                DateOpened = DateTime.Now,
                Description = "Ticket commessa",
                Numero = "T-100",
                CloseDescription = string.Empty,
                CloseNote = string.Empty
            },
            new Ticket
            {
                Id = 200,
                IdCompany = 1,
                IdUserOpened = ProductionTestContext.Utente,
                DateOpened = DateTime.Now,
                Description = "Ticket esterno",
                Numero = "T-200",
                CloseDescription = string.Empty,
                CloseNote = string.Empty
            });

        db.CommessaFaseTicketPlans.Add(new CommessaFaseTicketPlan
        {
            Id = 20,
            IdCommessaFase = 10,
            IdTicket = 100,
            IdTicketType = 1,
            Title = "Piano",
            AutoCreateMode = ProductionTicketAutoCreateMode.OnPhaseStart
        });
        db.TicketChats.Add(new TicketChat
        {
            Id = 30,
            IdTicket = 100,
            IdUser = ProductionTestContext.Utente,
            Date = DateTime.Now,
            Message = "Chat"
        });
        db.TicketChatReads.Add(new TicketChatRead
        {
            Id = 31,
            IdTicketChat = 30,
            IdUser = ProductionTestContext.Utente,
            DateRead = DateTime.Now
        });
        db.TicketsInterventions.Add(new TicketIntervention
        {
            Id = 40,
            IdTicket = 100,
            IdUser = ProductionTestContext.Utente,
            Activities = "Intervento",
            MountedParts = string.Empty,
            StartDateTime = DateTime.Now,
            EndDateTime = DateTime.Now.AddHours(1)
        });
        db.ExpenseReceipts.Add(new ExpenseReceipt
        {
            Id = 41,
            TicketInterventionId = 40,
            Description = "Nota spese"
        });
        db.TicketInterventionTimes.Add(new TicketInterventionTime
        {
            Id = 42,
            IdTicketIntervention = 40,
            StartDateTime = DateTime.Now,
            EndDateTime = DateTime.Now.AddHours(1)
        });
        db.TicketFeedbacks.Add(new TicketFeedback
        {
            Id = 50,
            IdTicket = 100,
            IdUser = ProductionTestContext.Utente,
            Rating = 5,
            Comment = "ok",
            CreatedAt = DateTime.Now
        });
        db.InboundEmails.Add(new InboundEmail
        {
            Id = 60,
            IdInbox = 1,
            IdTicket = 100,
            ReceivedAt = DateTime.Now
        });
        db.AssistantChatLogs.Add(new AssistantChatLog
        {
            Id = 70,
            Question = "q",
            Answer = "a",
            IdTicket = 100
        });
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(1);

        Assert.True(result.State);
        Assert.False(await db.Commesse.AnyAsync(c => c.Id == 1));
        Assert.False(await db.CommessaFasi.AnyAsync(f => f.IdCommessa == 1));
        Assert.False(await db.Tickets.AnyAsync(t => t.Id == 100));
        Assert.True(await db.Tickets.AnyAsync(t => t.Id == 200));
        Assert.False(await db.TicketChats.AnyAsync());
        Assert.False(await db.TicketChatReads.AnyAsync());
        Assert.False(await db.TicketsInterventions.AnyAsync());
        Assert.False(await db.ExpenseReceipts.AnyAsync());
        Assert.False(await db.TicketInterventionTimes.AnyAsync());
        Assert.False(await db.TicketFeedbacks.AnyAsync());
        Assert.Null((await db.InboundEmails.SingleAsync(e => e.Id == 60)).IdTicket);
        Assert.Null((await db.AssistantChatLogs.SingleAsync(l => l.Id == 70)).IdTicket);
    }
}
