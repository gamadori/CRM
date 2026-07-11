using System.Text.Json;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services.Email
{
    public class EmailEngagementService : IEmailEngagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;

        private const int MaxDetailLength = 1000;

        public EmailEngagementService(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        /// <summary>Evento normalizzato, indipendente dal provider.</summary>
        private sealed record NormalizedEvent(EmailProvider Provider, string MessageRef, EmailEventType Type, DateTime OccurredAt, string? Url, string? Detail);

        // ----------------------------------------------------------------- SendGrid

        public async Task<int> IngestSendGridAsync(JsonElement root, CancellationToken ct = default)
        {
            var events = new List<NormalizedEvent>();

            // Il webhook di SendGrid invia un array di eventi.
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in root.EnumerateArray())
                {
                    var messageRef = GetString(e, "ref");
                    if (string.IsNullOrEmpty(messageRef)) continue;

                    var type = MapSendGrid(GetString(e, "event"));
                    var occurredAt = FromUnix(GetLong(e, "timestamp"));
                    var url = GetString(e, "url");
                    var detail = GetString(e, "reason") ?? GetString(e, "response");

                    events.Add(new NormalizedEvent(EmailProvider.SendGrid, messageRef!, type, occurredAt, url, detail));
                }
            }

            return await ApplyAsync(events, ct);
        }

        private static EmailEventType MapSendGrid(string? ev) => ev switch
        {
            "delivered" => EmailEventType.Delivered,
            "open" => EmailEventType.Opened,
            "click" => EmailEventType.Clicked,
            "bounce" => EmailEventType.Bounced,
            "dropped" => EmailEventType.Dropped,
            "spamreport" => EmailEventType.SpamReported,
            "unsubscribe" or "group_unsubscribe" => EmailEventType.Unsubscribed,
            "deferred" => EmailEventType.Deferred,
            _ => EmailEventType.Other
        };

        // ----------------------------------------------------------------- Brevo

        public async Task<int> IngestBrevoAsync(JsonElement root, CancellationToken ct = default)
        {
            var events = new List<NormalizedEvent>();

            // Brevo invia un singolo oggetto evento; gestiamo anche un eventuale array.
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in root.EnumerateArray())
                    AddBrevoEvent(events, e);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                AddBrevoEvent(events, root);
            }

            return await ApplyAsync(events, ct);
        }

        private static void AddBrevoEvent(List<NormalizedEvent> events, JsonElement e)
        {
            var messageRef = ExtractBrevoTag(e);
            if (string.IsNullOrEmpty(messageRef)) return;

            var type = MapBrevo(GetString(e, "event"));
            var occurredAt = GetLong(e, "ts_event") is long tse and > 0 ? FromUnix(tse)
                : GetLong(e, "ts") is long ts and > 0 ? FromUnix(ts)
                : DateTime.Now;
            var url = GetString(e, "link");
            var detail = GetString(e, "reason");

            events.Add(new NormalizedEvent(EmailProvider.Brevo, messageRef!, type, occurredAt, url, detail));
        }

        private static string? ExtractBrevoTag(JsonElement e)
        {
            if (e.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String)
                return tag.GetString();

            if (e.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                foreach (var t in tags.EnumerateArray())
                    if (t.ValueKind == JsonValueKind.String)
                        return t.GetString();

            return null;
        }

        private static EmailEventType MapBrevo(string? ev) => ev switch
        {
            "delivered" => EmailEventType.Delivered,
            "opened" or "unique_opened" => EmailEventType.Opened,
            "click" => EmailEventType.Clicked,
            "hard_bounce" or "soft_bounce" or "blocked" => EmailEventType.Bounced,
            "spam" => EmailEventType.SpamReported,
            "unsubscribed" => EmailEventType.Unsubscribed,
            "deferred" => EmailEventType.Deferred,
            _ => EmailEventType.Other
        };

        // ----------------------------------------------------------------- Applicazione

        private async Task<int> ApplyAsync(List<NormalizedEvent> events, CancellationToken ct)
        {
            if (events.Count == 0) return 0;

            try
            {
                var refs = events.Select(x => x.MessageRef).Distinct().ToList();
                var emails = await _context.EmailsSent
                    .Where(x => x.MessageRef != null && refs.Contains(x.MessageRef))
                    .ToDictionaryAsync(x => x.MessageRef!, ct);

                int applied = 0;

                foreach (var ev in events)
                {
                    if (!emails.TryGetValue(ev.MessageRef, out var email))
                        continue; // email non trovata (es. inviata via SMTP puro): evento ignorato

                    _context.EmailEvents.Add(new EmailEvent
                    {
                        IdEmailSent = email.Id,
                        Provider = ev.Provider,
                        Type = ev.Type,
                        OccurredAt = ev.OccurredAt,
                        Url = ev.Url,
                        Detail = Truncate(ev.Detail, MaxDetailLength),
                        CreatedAt = DateTime.Now
                    });

                    UpdateSummary(email, ev);
                    applied++;
                }

                if (applied > 0)
                    await _context.SaveChangesAsync(ct);

                return applied;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailEngagementService), nameof(ApplyAsync), EventsTypes.Error, ex);
                return 0;
            }
        }

        private static void UpdateSummary(EmailSent email, NormalizedEvent ev)
        {
            email.LastEventAt = ev.OccurredAt;

            switch (ev.Type)
            {
                case EmailEventType.Delivered:
                    email.DeliveredAt ??= ev.OccurredAt;
                    Raise(email, EmailEngagementStatus.Delivered);
                    break;

                case EmailEventType.Opened:
                    email.OpenedAt ??= ev.OccurredAt;
                    email.OpenCount++;
                    Raise(email, EmailEngagementStatus.Opened);
                    break;

                case EmailEventType.Clicked:
                    email.OpenedAt ??= ev.OccurredAt; // un click implica l'apertura
                    email.LastClickedAt = ev.OccurredAt;
                    email.ClickCount++;
                    Raise(email, EmailEngagementStatus.Clicked);
                    break;

                case EmailEventType.Bounced:
                case EmailEventType.Dropped:
                    email.BouncedAt ??= ev.OccurredAt;
                    email.BounceReason = ev.Detail;
                    Raise(email, EmailEngagementStatus.Bounced);
                    break;

                case EmailEventType.SpamReported:
                    Raise(email, EmailEngagementStatus.SpamReported);
                    break;

                case EmailEventType.Unsubscribed:
                    Raise(email, EmailEngagementStatus.Unsubscribed);
                    break;
            }
        }

        // La progressione mantiene sempre lo stato più significativo (ordinamento dell'enum).
        private static void Raise(EmailSent email, EmailEngagementStatus status)
        {
            if ((int)status > (int)email.EngagementStatus)
                email.EngagementStatus = status;
        }

        // ----------------------------------------------------------------- Helpers JSON

        private static string? GetString(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static long GetLong(JsonElement e, string name)
        {
            if (e.TryGetProperty(name, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
                if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s)) return s;
            }
            return 0;
        }

        private static DateTime FromUnix(long seconds) =>
            seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime : DateTime.Now;

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
