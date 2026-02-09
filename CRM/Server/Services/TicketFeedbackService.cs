using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CRM.Server.Services
{
    /// <summary>
    /// Implementazione server del servizio TicketFeedback.
    /// Accede direttamente al database.
    /// </summary>
    public class TicketFeedbackService : ITicketFeedbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TicketFeedbackService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public async Task<List<TicketPendingFeedback>> GetPendingFeedbacksAsync()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return new List<TicketPendingFeedback>();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return new List<TicketPendingFeedback>();

            var pendingTickets = await _context.Tickets
                .Include(t => t.Company)
                .Where(t => t.Closed == true
                    && t.IdCompany == user.IdCompany
                    && !_context.TicketFeedbacks.Any(f => f.IdTicket == t.Id))
                .OrderByDescending(t => t.DateClosed)
                .Select(t => new TicketPendingFeedback
                {
                    TicketId = t.Id,
                    Description = t.Description,
                    DateClosed = t.DateClosed,
                    CloseDescription = t.CloseDescription,
                    Company = t.Company.RagioneSociale
                })
                .ToListAsync();

            return pendingTickets;
        }

        public async Task<int> GetPendingFeedbacksCountAsync()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return 0;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return 0;

            return await _context.Tickets
                .Where(t => t.Closed == true
                    && t.IdCompany == user.IdCompany
                    && !_context.TicketFeedbacks.Any(f => f.IdTicket == t.Id))
                .CountAsync();
        }

        public async Task<TicketFeedbackResponse> CreateFeedbackAsync(TicketFeedbackRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Utente non autenticato");

            var ticket = await _context.Tickets
                .Include(t => t.Company)
                .FirstOrDefaultAsync(t => t.Id == request.IdTicket);

            if (ticket == null)
                throw new KeyNotFoundException($"Ticket #{request.IdTicket} non trovato");

            if (!ticket.Closed)
                throw new InvalidOperationException("Il ticket non è ancora chiuso");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.IdCompany != ticket.IdCompany)
                throw new UnauthorizedAccessException("Non hai i permessi per lasciare un feedback su questo ticket");

            var existingFeedback = await _context.TicketFeedbacks
                .FirstOrDefaultAsync(f => f.IdTicket == request.IdTicket);

            if (existingFeedback != null)
                throw new InvalidOperationException("È già stato lasciato un feedback per questo ticket");

            var feedback = new TicketFeedback
            {
                IdTicket = request.IdTicket,
                Rating = request.Rating,
                Comment = request.Comment,
                IdUser = userId,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.TicketFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return new TicketFeedbackResponse
            {
                Id = feedback.Id,
                IdTicket = feedback.IdTicket,
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt,
                UserName = user.NameComplete
            };
        }

        public async Task<TicketFeedbackResponse?> GetFeedbackAsync(int id)
        {
            var feedback = await _context.TicketFeedbacks
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
                return null;

            return new TicketFeedbackResponse
            {
                Id = feedback.Id,
                IdTicket = feedback.IdTicket,
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt,
                UserName = feedback.User?.NameComplete ?? "N/A"
            };
        }

        public async Task<TicketFeedbackResponse?> GetFeedbackByTicketAsync(int ticketId)
        {
            var feedback = await _context.TicketFeedbacks
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.IdTicket == ticketId);

            if (feedback == null)
                return null;

            return new TicketFeedbackResponse
            {
                Id = feedback.Id,
                IdTicket = feedback.IdTicket,
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt,
                UserName = feedback.User?.NameComplete ?? "N/A"
            };
        }

        public async Task<bool> SkipFeedbackAsync(int ticketId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Utente non autenticato");

            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null)
                throw new KeyNotFoundException($"Ticket #{ticketId} non trovato");

            var existingFeedback = await _context.TicketFeedbacks
                .FirstOrDefaultAsync(f => f.IdTicket == ticketId);

            if (existingFeedback != null)
                throw new InvalidOperationException("Esiste già un feedback per questo ticket");

            var feedback = new TicketFeedback
            {
                IdTicket = ticketId,
                Rating = 0,
                Comment = "[Feedback saltato dall'utente]",
                IdUser = userId,
                CreatedAt = DateTime.Now,
                IsRead = true
            };

            _context.TicketFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<TicketFeedbackResponse>> GetAllFeedbacksAsync(bool unreadOnly = false)
        {
            var query = _context.TicketFeedbacks
                .Include(f => f.User)
                .Include(f => f.Ticket)
                    .ThenInclude(t => t.Company)
                .AsQueryable();

            if (unreadOnly)
                query = query.Where(f => !f.IsRead);

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new TicketFeedbackResponse
                {
                    Id = f.Id,
                    IdTicket = f.IdTicket,
                    Rating = f.Rating,
                    Comment = f.Comment,
                    CreatedAt = f.CreatedAt,
                    UserName = f.User.NameComplete
                })
                .ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(int id)
        {
            var feedback = await _context.TicketFeedbacks.FindAsync(id);
            if (feedback == null)
                return false;

            feedback.IsRead = true;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
