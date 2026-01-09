using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using WebPush;
using CRM.Client.Services;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio per gestire le notifiche push Web (browser)
    /// </summary>
    public interface IPushNotificationService
    {
        Task<bool> SaveSubscriptionAsync(string userId, string subscriptionJson);
        Task<bool> RemoveSubscriptionAsync(string userId);
        Task<int> SendToUserAsync(string userId, object notification);
        Task<int> SendToUsersAsync(List<string> userIds, object notification);
        Task<int> SendToAllAsync(object notification);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogEventService _logEventService;

        public PushNotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogEventService logEventService)
        {
            _context = context;
            _configuration = configuration;
            _logEventService = logEventService;
        }

        /// <summary>
        /// Salva o aggiorna la subscription di un utente
        /// </summary>
        public async Task<bool> SaveSubscriptionAsync(string userId, string subscriptionJson)
        {
            try
            {
                // ? LOG: Debug subscription JSON ricevuta
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SaveSubscriptionAsync),
                    LogEvent.EventsTypes.Info,
                    $"DEBUG: Ricevuta subscription per user {userId}. JSON length: {subscriptionJson?.Length ?? 0}. Preview: {subscriptionJson?.Substring(0, Math.Min(200, subscriptionJson?.Length ?? 0))}...");
                
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SaveSubscriptionAsync),
                        LogEvent.EventsTypes.Error,
                        $"? Utente {userId} non trovato nel database");
                    return false;
                }

                // Salva subscription (serializzata) su campo utente
                user.PushSubscription = subscriptionJson;
                user.PushSubscriptionDate = DateTime.Now;

                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SaveSubscriptionAsync),
                    LogEvent.EventsTypes.Info,
                    $"? Push subscription salvata per utente {user.NameComplete}");

                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SaveSubscriptionAsync),
                    LogEvent.EventsTypes.Error,
                    ex);
                return false;
            }
        }

        /// <summary>
        /// Rimuove la subscription di un utente
        /// </summary>
        public async Task<bool> RemoveSubscriptionAsync(string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                user.PushSubscription = null;
                user.PushSubscriptionDate = null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(RemoveSubscriptionAsync),
                    LogEvent.EventsTypes.Error,
                    ex);
                return false;
            }
        }

        /// <summary>
        /// Invia notifica push a un singolo utente
        /// </summary>
        public async Task<int> SendToUserAsync(string userId, object notification)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.PushSubscription))
                return 0;

            return await SendPushNotification(user.PushSubscription, notification);
        }

        /// <summary>
        /// Invia notifica push a più utenti
        /// </summary>
        public async Task<int> SendToUsersAsync(List<string> userIds, object notification)
        {
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id) && !string.IsNullOrEmpty(u.PushSubscription))
                .ToListAsync();

            int successCount = 0;

            foreach (var user in users)
            {
                var result = await SendPushNotification(user.PushSubscription, notification);
                if (result > 0)
                    successCount++;
            }

            return successCount;
        }

        /// <summary>
        /// Invia notifica push a TUTTI gli utenti con subscription attiva
        /// </summary>
        public async Task<int> SendToAllAsync(object notification)
        {
            var users = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.PushSubscription))
                .ToListAsync();

            int successCount = 0;

            foreach (var user in users)
            {
                var result = await SendPushNotification(user.PushSubscription, notification);
                if (result > 0)
                    successCount++;
            }

            return successCount;
        }

        /// <summary>
        /// ? Invia effettivamente la push notification usando WebPush library
        /// </summary>
        private async Task<int> SendPushNotification(string subscriptionJson, object notificationData)
        {
            try
            {
                // ? LOG 1: Inizio invio (solo primi 100 caratteri per non loggare dati sensibili)
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Info,
                    $"Tentativo invio push notification. Subscription length: {subscriptionJson?.Length ?? 0}");
                
                // Leggi VAPID keys da configurazione
                var vapidPublicKey = _configuration["PushNotifications:WebPush:publicKey"];
                var vapidPrivateKey = _configuration["PushNotifications:WebPush:privateKey"];
                var vapidSubject = _configuration["PushNotifications:WebPush:subject"];

                // ? LOG 2: Verifica VAPID keys (solo primi caratteri per sicurezza)
                if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        "? VAPID keys non configurate in appsettings.json - Verifica sezione WebPush");
                    return 0;
                }

                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Info,
                    $"? VAPID keys trovate - Subject: {vapidSubject}, PublicKey prefix: {vapidPublicKey?.Substring(0, Math.Min(20, vapidPublicKey.Length))}...");

                // Deserializza subscription con DTO intermedio per gestire struttura nested
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                
                // DTO che corrisponde alla struttura JSON del browser
                var subscriptionDto = JsonSerializer.Deserialize<PushSubscriptionDto>(subscriptionJson, jsonOptions);
                
                if (subscriptionDto == null || string.IsNullOrEmpty(subscriptionDto.Endpoint))
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        "? Impossibile deserializzare subscription - JSON potrebbe essere malformato");
                    return 0;
                }

                // Mappa a WebPush.PushSubscription
                var subscription = new WebPush.PushSubscription
                {
                    Endpoint = subscriptionDto.Endpoint,
                    Auth = subscriptionDto.Keys?.Auth,
                    P256DH = subscriptionDto.Keys?.P256dh
                };

                if (subscription == null)
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        "? Impossibile mappare subscription a WebPush.PushSubscription");
                    return 0;
                }

                // ? LOG 3: Subscription deserializzata con successo
                var endpointPreview = subscription.Endpoint?.Substring(0, Math.Min(60, subscription.Endpoint.Length)) ?? "N/A";
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Info,
                    $"? Subscription deserializzata - Endpoint: {endpointPreview}...");

                // ? DEBUG: Log JSON completo se endpoint è null
                if (string.IsNullOrEmpty(subscription.Endpoint))
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        $"? DEBUG: Endpoint è NULL! JSON originale: {subscriptionJson.Substring(0, Math.Min(300, subscriptionJson.Length))}");
                    return 0;
                }

                // Serializza payload
                var payload = JsonSerializer.Serialize(notificationData);

                // ? LOG 4: Payload serializzato
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Info,
                    $"? Payload preparato - Length: {payload.Length} bytes");

                // Crea client WebPush
                var webPushClient = new WebPushClient();

                // ? LOG 5: Invio in corso
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Info,
                    "?? Invio notifica push via WebPushClient...");

                // Invia notifica
                await webPushClient.SendNotificationAsync(
                    subscription,
                    payload,
                    new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey));

                // ? LOG 6: Successo!
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Info,
                    "? Notifica push inviata con SUCCESSO!");

                return 1; // Successo
            }
            catch (WebPushException ex)
            {
                // ? LOG 7: Errore WebPush specifico
                var errorDetails = $"WebPushException - StatusCode: {ex.StatusCode}, Message: {ex.Message}";
                
                // Gestisci errori specifici
                if (ex.StatusCode == System.Net.HttpStatusCode.Gone || 
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Subscription scaduta o invalida ? rimuovila
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Warning,
                        $"?? Subscription scaduta/invalida (HTTP {(int)ex.StatusCode}) - L'utente deve riattivare le notifiche. {errorDetails}");
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        $"? VAPID keys INVALIDE (HTTP 401) - Rigenera le chiavi VAPID. {errorDetails}");
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        $"? Richiesta malformata (HTTP 400) - Verifica formato subscription e payload. {errorDetails}");
                }
                else
                {
                    await _logEventService.RegisterAsync(
                        nameof(PushNotificationService),
                        nameof(SendPushNotification),
                        LogEvent.EventsTypes.Error,
                        $"? Errore WebPush generico: {errorDetails}");
                }
                return 0;
            }
            catch (JsonException jsonEx)
            {
                // ? LOG 8: Errore JSON serialization
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Error,
                    $"? Errore deserializzazione JSON: {jsonEx.Message}");
                return 0;
            }
            catch (Exception ex)
            {
                // ? LOG 9: Errore generico
                await _logEventService.RegisterAsync(
                    nameof(PushNotificationService),
                    nameof(SendPushNotification),
                    LogEvent.EventsTypes.Error,
                    $"? Errore imprevisto [{ex.GetType().Name}]: {ex.Message}");
                return 0;
            }
        }
    }

    /// <summary>
    /// DTO per deserializzare la subscription JSON del browser
    /// che ha una struttura nested con "keys"
    /// </summary>
    internal class PushSubscriptionDto
    {
        public string Endpoint { get; set; }
        public long? ExpirationTime { get; set; }
        public PushSubscriptionKeys Keys { get; set; }
    }

    /// <summary>
    /// DTO per le chiavi nested dentro "keys"
    /// </summary>
    internal class PushSubscriptionKeys
    {
        public string Auth { get; set; }
        public string P256dh { get; set; }
    }
}
