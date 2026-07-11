using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class WorkflowAutomationService : IWorkflowAutomationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public WorkflowAutomationService(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<WorkflowAutomation?> GetItemAsync(int id)
        {
            return await _context.WorkflowAutomations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<PagingResponse<WorkflowAutomation>?> GetPagingAsync(WorkflowAutomationFilter? args = null)
        {
            try
            {
                var q = FilterItems(args);
                var count = await q.CountAsync();

                if (args?.Skip != null && args.Top != null)
                {
                    q = q.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                return new PagingResponse<WorkflowAutomation>
                {
                    Items = await q.AsNoTracking().ToListAsync(),
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args?.PageSize ?? 0 },
                    Total = string.Empty
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(GetPagingAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<WorkflowAutomation>?> GetListAsync(WorkflowAutomationFilter? args = null)
        {
            try
            {
                return await FilterItems(args).AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<WorkflowAutomationExecutionDTO>?> GetExecutionsAsync(WorkflowAutomationExecutionFilter? args = null)
        {
            try
            {
                var q = FilterExecutions(args);
                var count = await q.CountAsync();

                if (args?.Skip != null && args.Top != null)
                {
                    q = q.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var executions = await q.AsNoTracking().ToListAsync();
                var names = await ResolveEntityNamesAsync(executions);

                return new PagingResponse<WorkflowAutomationExecutionDTO>
                {
                    Items = executions.Select(x => new WorkflowAutomationExecutionDTO
                    {
                        Id = x.Id,
                        IdWorkflowAutomation = x.IdWorkflowAutomation,
                        RuleName = x.WorkflowAutomation?.Name ?? "Regola eliminata",
                        Trigger = x.Trigger,
                        EntityType = x.EntityType,
                        EntityId = x.EntityId,
                        EntityName = names.GetValueOrDefault((x.EntityType, x.EntityId), $"#{x.EntityId}"),
                        ExecutedAt = x.ExecutedAt,
                        IdActivity = x.IdActivity,
                        ActivitySubject = x.Activity?.Subject ?? string.Empty,
                        ActivityKind = x.Activity?.Kind,
                        Error = x.Error
                    }).ToList(),
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args?.PageSize ?? 0 },
                    Total = string.Empty
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(GetExecutionsAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<WorkflowAutomation>> PostAsync(WorkflowAutomation item)
        {
            try
            {
                if (item.Id > 0)
                {
                    var existing = await _context.WorkflowAutomations.FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (existing == null)
                    {
                        return Fail("Automazione non trovata", System.Net.HttpStatusCode.NotFound);
                    }

                    existing.Name = item.Name;
                    existing.IsActive = item.IsActive;
                    existing.Trigger = item.Trigger;
                    existing.ActionType = item.ActionType;
                    existing.MinAmount = item.MinAmount;
                    existing.LeadSource = item.LeadSource;
                    existing.LeadStatus = item.LeadStatus;
                    existing.DealState = item.DealState;
                    existing.DealPhase = item.DealPhase;
                    existing.ActivityKind = item.ActivityKind;
                    existing.ActivitySubject = item.ActivitySubject;
                    existing.ActivityDescription = item.ActivityDescription;
                    existing.DueDays = item.DueDays;
                    existing.AssignToOwner = item.AssignToOwner;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    item.CreatedAt = DateTime.Now;
                    _context.WorkflowAutomations.Add(item);
                }

                await _context.SaveChangesAsync();
                return Ok(item, "Automazione salvata");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio dell'automazione", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.WorkflowAutomations.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            try
            {
                _context.WorkflowAutomations.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task ExecuteAsync(WorkflowTrigger trigger, Lead? lead = null, Deal? deal = null)
        {
            try
            {
                var rules = await _context.WorkflowAutomations
                    .Where(x => x.IsActive && x.Trigger == trigger)
                    .ToListAsync();

                foreach (var rule in rules.Where(r => Matches(r, lead, deal)))
                {
                    await ExecuteRuleAsync(rule, trigger, lead, deal);
                }

                if (rules.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(ExecuteAsync), EventsTypes.Error, ex);
            }
        }

        public async Task<int> ExecutePendingAsync(int maxItems, CancellationToken cancellationToken = default)
        {
            try
            {
                var rules = await _context.WorkflowAutomations
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.LastRunAt ?? x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .Take(Math.Clamp(maxItems, 1, 200))
                    .ToListAsync(cancellationToken);

                var executed = 0;
                foreach (var rule in rules)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var candidates = await LoadPendingCandidatesAsync(rule, cancellationToken);
                    foreach (var candidate in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (await ExecuteRuleAsync(rule, rule.Trigger, candidate.Lead, candidate.Deal, cancellationToken))
                        {
                            executed++;
                            if (executed >= maxItems)
                            {
                                await _context.SaveChangesAsync(cancellationToken);
                                return executed;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                return executed;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(ExecutePendingAsync), EventsTypes.Error, ex);
                return 0;
            }
        }

        private async Task<bool> ExecuteRuleAsync(
            WorkflowAutomation rule,
            WorkflowTrigger trigger,
            Lead? lead,
            Deal? deal,
            CancellationToken cancellationToken = default)
        {
            var entity = ResolveEntity(lead, deal);
            if (entity == null || entity.Value.EntityId <= 0)
            {
                return false;
            }

            if (await HasExecutionAsync(rule.Id, trigger, entity.Value.EntityType, entity.Value.EntityId, cancellationToken))
            {
                return false;
            }

            Activity? activity = null;
            string? error = null;

            try
            {
                if (rule.ActionType == WorkflowActionType.CreateActivity)
                {
                    activity = await CreateActivityAsync(rule, lead, deal);
                }

                rule.LastRunAt = DateTime.Now;
                rule.ExecutionCount++;
            }
            catch (Exception ex)
            {
                error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                await _logEventService.RegisterAsync(nameof(WorkflowAutomationService), nameof(ExecuteRuleAsync), EventsTypes.Error, ex);
            }

            _context.WorkflowAutomationExecutions.Add(new WorkflowAutomationExecution
            {
                IdWorkflowAutomation = rule.Id,
                Trigger = trigger,
                EntityType = entity.Value.EntityType,
                EntityId = entity.Value.EntityId,
                ExecutedAt = DateTime.Now,
                Activity = activity,
                Error = error
            });

            return error == null;
        }

        private async Task<Activity?> CreateActivityAsync(WorkflowAutomation rule, Lead? lead, Deal? deal)
        {
            var idUser = rule.AssignToOwner
                ? (lead?.IdUser ?? deal?.IdUser ?? await SafeCurrentUserAsync())
                : await SafeCurrentUserAsync();

            var activity = new Activity
            {
                Kind = rule.ActivityKind,
                Subject = Render(rule.ActivitySubject, lead, deal),
                Description = Render(rule.ActivityDescription, lead, deal),
                EntityType = lead != null ? ActivityEntityType.Lead : ActivityEntityType.Deal,
                EntityId = lead?.Id ?? deal?.Id ?? 0,
                IdUser = await SafeCurrentUserAsync(),
                IdAssignee = idUser,
                DueDate = DateTime.Today.AddDays(rule.DueDays),
                ReminderAt = DateTime.Today.AddDays(rule.DueDays),
                State = ActivityState.Planned,
                CreatedAt = DateTime.Now
            };

            if (activity.EntityId > 0)
            {
                _context.Activities.Add(activity);
                return activity;
            }

            return null;
        }

        private async Task<List<AutomationCandidate>> LoadPendingCandidatesAsync(WorkflowAutomation rule, CancellationToken cancellationToken)
        {
            return rule.Trigger switch
            {
                WorkflowTrigger.LeadCreated => await PendingLeads(rule, LeadStatus.New, cancellationToken),
                WorkflowTrigger.LeadQualified => await PendingLeads(rule, LeadStatus.Qualified, cancellationToken),
                WorkflowTrigger.LeadConverted => await PendingLeads(rule, LeadStatus.Converted, cancellationToken),
                WorkflowTrigger.DealCreated => await PendingDeals(rule, null, cancellationToken),
                WorkflowTrigger.DealWon => await PendingDeals(rule, DealStates.CloseWon, cancellationToken),
                WorkflowTrigger.DealLost => await PendingDeals(rule, DealStates.CloseLost, cancellationToken),
                _ => new List<AutomationCandidate>()
            };
        }

        private async Task<List<AutomationCandidate>> PendingLeads(WorkflowAutomation rule, LeadStatus status, CancellationToken cancellationToken)
        {
            var q = _context.Leads
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.User)
                .Where(x => x.Status == status)
                .Where(x => !_context.WorkflowAutomationExecutions.Any(e =>
                    e.IdWorkflowAutomation == rule.Id &&
                    e.Trigger == rule.Trigger &&
                    e.EntityType == ActivityEntityType.Lead &&
                    e.EntityId == x.Id));

            q = rule.Trigger switch
            {
                WorkflowTrigger.LeadCreated => q.Where(x => x.CreatedAt >= rule.CreatedAt),
                WorkflowTrigger.LeadQualified => q.Where(x => (x.UpdatedAt ?? x.CreatedAt) >= rule.CreatedAt),
                WorkflowTrigger.LeadConverted => q.Where(x => (x.ConvertedAt ?? x.UpdatedAt ?? x.CreatedAt) >= rule.CreatedAt),
                _ => q
            };

            var leads = await q
                .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            return leads
                .Where(lead => Matches(rule, lead, null))
                .Select(lead => new AutomationCandidate(lead, null))
                .ToList();
        }

        private async Task<List<AutomationCandidate>> PendingDeals(WorkflowAutomation rule, DealStates? state, CancellationToken cancellationToken)
        {
            var q = _context.Deals
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.User)
                .Where(x => !_context.WorkflowAutomationExecutions.Any(e =>
                    e.IdWorkflowAutomation == rule.Id &&
                    e.Trigger == rule.Trigger &&
                    e.EntityType == ActivityEntityType.Deal &&
                    e.EntityId == x.Id));

            if (state != null)
            {
                q = q.Where(x => x.State == state);
            }

            q = rule.Trigger switch
            {
                WorkflowTrigger.DealCreated => q.Where(x => x.Date.Date >= rule.CreatedAt.Date),
                WorkflowTrigger.DealWon => q.Where(x => x.DateClosed != default && x.DateClosed.Date >= rule.CreatedAt.Date),
                WorkflowTrigger.DealLost => q.Where(x => x.DateClosed != default && x.DateClosed.Date >= rule.CreatedAt.Date),
                _ => q
            };

            var deals = await q
                .OrderByDescending(x => x.Date)
                .Take(50)
                .ToListAsync(cancellationToken);

            return deals
                .Where(deal => Matches(rule, null, deal))
                .Select(deal => new AutomationCandidate(null, deal))
                .ToList();
        }

        private async Task<bool> HasExecutionAsync(
            int ruleId,
            WorkflowTrigger trigger,
            ActivityEntityType entityType,
            int entityId,
            CancellationToken cancellationToken)
        {
            return await _context.WorkflowAutomationExecutions.AnyAsync(x =>
                x.IdWorkflowAutomation == ruleId &&
                x.Trigger == trigger &&
                x.EntityType == entityType &&
                x.EntityId == entityId,
                cancellationToken);
        }

        private static (ActivityEntityType EntityType, int EntityId)? ResolveEntity(Lead? lead, Deal? deal)
        {
            if (lead != null)
            {
                return (ActivityEntityType.Lead, lead.Id);
            }

            if (deal != null)
            {
                return (ActivityEntityType.Deal, deal.Id);
            }

            return null;
        }

        private static bool Matches(WorkflowAutomation rule, Lead? lead, Deal? deal)
        {
            if (lead != null)
            {
                if (rule.LeadSource != null && lead.Source != rule.LeadSource) return false;
                if (rule.LeadStatus != null && lead.Status != rule.LeadStatus) return false;
                if (rule.MinAmount != null && lead.EstimatedValue < rule.MinAmount) return false;
            }

            if (deal != null)
            {
                if (rule.DealState != null && deal.State != rule.DealState) return false;
                if (rule.DealPhase != null && deal.Phase != rule.DealPhase) return false;
                if (rule.MinAmount != null && deal.Amount < rule.MinAmount) return false;
            }

            return true;
        }

        private IQueryable<WorkflowAutomation> FilterItems(WorkflowAutomationFilter? args)
        {
            var q = _context.WorkflowAutomations.AsQueryable();

            if (!string.IsNullOrWhiteSpace(args?.Search))
            {
                var search = args.Search.Trim();
                q = q.Where(x => x.Name.Contains(search) || x.ActivitySubject.Contains(search));
            }

            if (args?.IsActive != null)
            {
                q = q.Where(x => x.IsActive == args.IsActive);
            }

            if (args?.Trigger != null)
            {
                q = q.Where(x => x.Trigger == args.Trigger);
            }

            return q.OrderByDescending(x => x.IsActive).ThenBy(x => x.Trigger).ThenBy(x => x.Name);
        }

        private IQueryable<WorkflowAutomationExecution> FilterExecutions(WorkflowAutomationExecutionFilter? args)
        {
            var q = _context.WorkflowAutomationExecutions
                .Include(x => x.WorkflowAutomation)
                .Include(x => x.Activity)
                .AsQueryable();

            if (args?.IdWorkflowAutomation != null)
            {
                q = q.Where(x => x.IdWorkflowAutomation == args.IdWorkflowAutomation);
            }

            if (args?.Trigger != null)
            {
                q = q.Where(x => x.Trigger == args.Trigger);
            }

            if (args?.EntityType != null)
            {
                q = q.Where(x => x.EntityType == args.EntityType);
            }

            if (args?.Succeeded != null)
            {
                q = args.Succeeded.Value
                    ? q.Where(x => x.Error == null || x.Error == string.Empty)
                    : q.Where(x => x.Error != null && x.Error != string.Empty);
            }

            if (args?.DateFrom != null)
            {
                q = q.Where(x => x.ExecutedAt >= args.DateFrom.Value);
            }

            if (args?.DateTo != null)
            {
                q = q.Where(x => x.ExecutedAt <= args.DateTo.Value);
            }

            return q.OrderByDescending(x => x.ExecutedAt).ThenByDescending(x => x.Id);
        }

        private async Task<Dictionary<(ActivityEntityType EntityType, int EntityId), string>> ResolveEntityNamesAsync(List<WorkflowAutomationExecution> executions)
        {
            var names = new Dictionary<(ActivityEntityType EntityType, int EntityId), string>();

            var leadIds = executions
                .Where(x => x.EntityType == ActivityEntityType.Lead)
                .Select(x => x.EntityId)
                .Distinct()
                .ToList();

            if (leadIds.Count > 0)
            {
                var leads = await _context.Leads
                    .AsNoTracking()
                    .Where(x => leadIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })
                    .ToListAsync();

                foreach (var lead in leads)
                {
                    names[(ActivityEntityType.Lead, lead.Id)] = lead.Name;
                }
            }

            var dealIds = executions
                .Where(x => x.EntityType == ActivityEntityType.Deal)
                .Select(x => x.EntityId)
                .Distinct()
                .ToList();

            if (dealIds.Count > 0)
            {
                var deals = await _context.Deals
                    .AsNoTracking()
                    .Where(x => dealIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })
                    .ToListAsync();

                foreach (var deal in deals)
                {
                    names[(ActivityEntityType.Deal, deal.Id)] = deal.Name;
                }
            }

            return names;
        }

        private async Task<string?> SafeCurrentUserAsync()
        {
            try { return await _permitsService.IdUser(); }
            catch { return null; }
        }

        private static string? Render(string? text, Lead? lead, Deal? deal)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            return text
                .Replace("{LeadName}", lead?.Name ?? string.Empty)
                .Replace("{DealName}", deal?.Name ?? string.Empty)
                .Replace("{Company}", lead?.Company?.RagioneSociale ?? lead?.CompanyName ?? deal?.Company?.RagioneSociale ?? string.Empty)
                .Replace("{Amount}", (lead?.EstimatedValue ?? deal?.Amount ?? 0).ToString("C"));
        }

        private static APIResponseMessage<WorkflowAutomation> Fail(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static APIResponseMessage<WorkflowAutomation> Ok(WorkflowAutomation? data, string msg)
            => new() { State = true, Data = data, Message = msg, Code = System.Net.HttpStatusCode.OK };

        private sealed record AutomationCandidate(Lead? Lead, Deal? Deal);
    }
}
