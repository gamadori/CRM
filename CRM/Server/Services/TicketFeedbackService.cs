using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using static CRM.Shared.LogEvent;

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
        private readonly IPermitsService _permitsService;
        public TicketFeedbackService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
        }

        public async Task<TicketFeedbackResponse?> GetItemAsync(int id)
        {
            var item = await _context.TicketFeedbacks
                .Include(x => x.User)
                .Include(x => x.Ticket)
                    .ThenInclude(x => x.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new TicketFeedbackResponse
            {
                Id = item.Id,
                IdTicket = item.IdTicket,
                User = item.User?.NameComplete ?? "N/A",
                Company = item.Ticket?.Company?.RagioneSociale ?? "N/A",
                Rating = item.Rating,
                Comment = item.Comment,
                CreatedAt = item.CreatedAt,
                UserName = item.User?.NameComplete ?? "N/A"
            } : null;
        }

        public async Task<TicketFeedbackResponse?> GetFirstAsync()
        {
            var item = await _context.TicketFeedbacks
                .Include(x => x.User)
                .Include(x => x.Ticket)
                    .ThenInclude(x => x.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new TicketFeedbackResponse()
            {
                Id = item.Id,
                IdTicket = item.IdTicket,
                User = item.User?.NameComplete ?? "N/A",
                Company = item.Ticket?.Company?.RagioneSociale ?? "N/A",
                Rating = item.Rating,
                Comment = item.Comment,
                CreatedAt = item.CreatedAt,
                UserName = item.User?.NameComplete ?? "N/A"
            } : null;
        }

        public async Task<PagingResponse<TicketFeedbackResponse, object>?> GetSummaryAsync(TicketFeedbackFilterModel? args)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new();
                }
                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,


                };
                PagingResponse<TicketFeedbackResponse, object> resp = new PagingResponse<TicketFeedbackResponse, object>()
                {
                    Items = await items.Select(item => new TicketFeedbackResponse
                    {
                        Id = item.Id,
                        IdTicket = item.IdTicket,
                        User = item.User!.NameComplete ?? "N/A",
                        Company = item.Ticket!.Company!.RagioneSociale ?? "N/A",
                        Rating = item.Rating,
                        Comment = item.Comment,
                        CreatedAt = item.CreatedAt,
                       
                    }).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {

                return null;
            }
        }

        public async Task<PagingResponse<TicketFeedbackResponse>?> GetPagingAsync(TicketFeedbackFilterModel? args = null)
        {
            try
            {

                var items = FilterItems(args);

                if (items == null)
                {
                    return new();
                }
                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,


                };
                PagingResponse<TicketFeedbackResponse> resp = new PagingResponse<TicketFeedbackResponse>()
                {
                    Items = await items.Select(item => new TicketFeedbackResponse
                    {
                        Id = item.Id,
                        IdTicket = item.IdTicket,
                        User = item.User!.NameComplete ?? "N/A",
                        Company = item.Ticket!.Company!.RagioneSociale ?? "N/A",
                        Rating = item.Rating,
                        Comment = item.Comment,
                        CreatedAt = item.CreatedAt,
                       
                    }).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<TicketFeedbackResponse>?> GetListAsync(TicketFeedbackFilterModel? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<TicketFeedbackResponse>();
                }

                return await items.Select(item => new TicketFeedbackResponse
                {
                    Id = item.Id,
                    IdTicket = item.IdTicket,
                    User = item.User!.NameComplete ?? "N/A",
                    Company = item.Ticket!.Company!.RagioneSociale ?? "N/A",
                    Rating = item.Rating,
                    Comment = item.Comment,
                    CreatedAt = item.CreatedAt,
                   
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<TicketFeedbackResponse>> PostAsync(TicketFeedback item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.TicketFeedbacks.Update(item);
                }
                else
                {
                    _context.TicketFeedbacks.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<TicketFeedbackResponse>
                {
                    State = true,
                    Data = new TicketFeedbackResponse
                    {
                        Id = item.Id,
                        IdTicket = item.IdTicket,
                        User = item.User!.NameComplete ?? "N/A",
                        Company = item.Ticket!.Company!.RagioneSociale ?? "N/A",
                        Rating = item.Rating,
                        Comment = item.Comment,
                        CreatedAt = item.CreatedAt,
                    },
                    Message = "Logo Event saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<TicketFeedbackResponse>
                {
                    State = false,
                    Message = "Error saving TicketFeedback",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.TicketFeedbacks.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.TicketFeedbacks.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<string?> GetCurrentUserId()
        {
            return await _permitsService.IdUser();
        }

        public async Task<List<TicketPendingFeedback>> GetPendingFeedbacksAsync()
        {
            var userId = await GetCurrentUserId();
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
            var userId = await GetCurrentUserId();
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
            var userId = await GetCurrentUserId();
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
            var userId = await GetCurrentUserId();
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

        public async Task<AverageFeedbackDTO> AverageRateAsync()
        {
            AverageFeedbackDTO feedback = new AverageFeedbackDTO();
            string? userId = await GetCurrentUserId();
            var companies = await _permitsService.GetIdCompanies();

            if (userId == null || companies == null || !companies.Any())
            {
                return new AverageFeedbackDTO
                {
                    Companies = new List<AverageFeedbackItemDTO<int>>(),
                    Users = new List<AverageFeedbackItemDTO<string>>()
                };
            }
            feedback.Companies = _context.TicketFeedbacks.Where(f => f.Rating > 0 && f.Ticket.IdCompanyAssigned.HasValue && companies.Contains(f.Ticket.IdCompanyAssigned.Value))
                .GroupBy(g => g.Ticket.IdCompanyAssigned).Select(g => new AverageFeedbackItemDTO<int>
                {
                    Id = g.Key.HasValue ? g.Key.Value : 0,
                    Name = g.Key.HasValue ? _context.Companies.Where(c => c.Id == g.Key.Value).Select(c => c.RagioneSociale).FirstOrDefault() ?? "N/A" : "N/A",
                    Average = (decimal)g.Average(f => f.Rating),
                    TotalFeedbacks = g.Count()
                }).ToList();

            
            
            // Media feedback per l'utente corrente
            var userFeedbacks = _context.TicketFeedbacks
                .Where(f => f.Rating > 0 && f.Ticket.AssignedUsers.Any(a => a.IdUser == userId));

            if (await userFeedbacks.AnyAsync())
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                feedback.Users.Add(new AverageFeedbackItemDTO<string>
                {
                    Id = userId,        
                    Name = user?.NameComplete ?? "N/A",
                    Average = (decimal)await userFeedbacks.AverageAsync(f => f.Rating),
                    TotalFeedbacks = await userFeedbacks.CountAsync()
                });
            }
                
            return feedback;
        }

        private IQueryable<TicketFeedback>? FilterItems(TicketFeedbackFilterModel? args = null)
        {
            try
            {
                var items = _context.TicketFeedbacks
                    .Include(x => x.User)
                    .Include(x => x.Ticket)
                        .ThenInclude(x => x.Company)
                    .AsQueryable();
                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.CreatedAt);

                if (args.IsRead != null)
                {
                    items = items.Where(x => x.IsRead == args.IsRead);
                }

                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
