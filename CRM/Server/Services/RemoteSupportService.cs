using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CRM.Server.Services
{
    public interface IRemoteSupportService
    {
        bool IsConfigured { get; }
        Task<RemoteSupportCodeDTO> IssueCodeAsync(int idArticle, string actor, CancellationToken ct = default);
        Task<RemoteSupportStatusDTO> StatusAsync(int idArticle, CancellationToken ct = default);
        Task RevokeAsync(int idArticle, string actor, CancellationToken ct = default);
        Task<RemoteSupportStartResultDTO> StartAsync(IEnumerable<int> idArticles, string actor, CancellationToken ct = default);
    }

    public sealed class RemoteSupportOptions
    {
        public const string SectionName = "RemoteSupport";

        /// <summary>Base HTTPS del servizio di provisioning sul VPS, es. https://service.bluegrape.net/provisioning.</summary>
        public string ProvisioningUrl { get; set; } = string.Empty;

        /// <summary>Chiave amministrativa del provisioning (Bearer). Solo lato server, mai al client.</summary>
        public string ProvisioningApiKey { get; set; } = string.Empty;

        /// <summary>Base HTTPS di Guacamole, es. https://service.bluegrape.net/guacamole.</summary>
        public string GuacamoleUrl { get; set; } = string.Empty;

        /// <summary>Chiave a 32 esadecimali condivisa con guacamole-auth-json sul VPS.</summary>
        public string JsonSecretKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Fa da portale di assistenza remota per le macchine BM: chiede al provisioning
    /// sul VPS i codici di abbinamento, lo stato e la connessione, e costruisce il
    /// token Guacamole per aprire lo schermo del pannello. Non conosce WireGuard né
    /// x11vnc; non salva password VNC né chiavi (restano sul VPS).
    /// </summary>
    public sealed class RemoteSupportService : IRemoteSupportService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _clients;
        private readonly RemoteSupportOptions _options;

        public RemoteSupportService(
            ApplicationDbContext db,
            IHttpClientFactory clients,
            IConfiguration configuration)
        {
            _db = db;
            _clients = clients;
            _options = configuration.GetSection(RemoteSupportOptions.SectionName).Get<RemoteSupportOptions>()
                ?? new RemoteSupportOptions();
        }

        public bool IsConfigured =>
            Uri.TryCreate(_options.ProvisioningUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo)
            && _options.ProvisioningApiKey.Length >= 40
            && GuacamoleToken.IsValidKey(_options.JsonSecretKey)
            && Uri.TryCreate(_options.GuacamoleUrl, UriKind.Absolute, out _);

        public async Task<RemoteSupportCodeDTO> IssueCodeAsync(int idArticle, string actor, CancellationToken ct = default)
        {
            EnsureConfigured();
            var deviceId = await EnsureDeviceAsync(idArticle, ct);
            var response = await AdminAsync<IssueResponse>("issue", deviceId, actor, ct);
            return new RemoteSupportCodeDTO { Code = response.Code, ExpiresAt = response.ExpiresAt };
        }

        public async Task<RemoteSupportStatusDTO> StatusAsync(int idArticle, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return new RemoteSupportStatusDTO { State = "Disabled" };

            var deviceId = await FindDeviceAsync(idArticle, ct);
            if (deviceId is null)
                return new RemoteSupportStatusDTO { State = "NotConfigured" };

            var response = await AdminAsync<StatusResponse>("status", deviceId.Value, actor: "portal", ct);
            return new RemoteSupportStatusDTO { State = response.State, Address = response.Address };
        }

        public async Task RevokeAsync(int idArticle, string actor, CancellationToken ct = default)
        {
            EnsureConfigured();
            var deviceId = await FindDeviceAsync(idArticle, ct)
                ?? throw new InvalidOperationException("Questa macchina non è registrata per l'assistenza remota.");
            await AdminAsync<RevokeResponse>("revoke", deviceId, actor, ct);
        }

        public async Task<RemoteSupportStartResultDTO> StartAsync(IEnumerable<int> idArticles, string actor, CancellationToken ct = default)
        {
            EnsureConfigured();

            var ids = idArticles.Distinct().ToList();
            var result = new RemoteSupportStartResultDTO();
            var connections = new Dictionary<string, object>();

            foreach (var idArticle in ids)
            {
                var deviceId = await FindDeviceAsync(idArticle, ct);
                if (deviceId is null)
                {
                    result.Skipped.Add(idArticle);
                    continue;
                }

                var connection = await AdminAsync<ConnectionResponse>("connection", deviceId.Value, actor, ct);
                if (connection.State != "Ready"
                    || connection.Protocol != "vnc"
                    || string.IsNullOrEmpty(connection.Address)
                    || string.IsNullOrEmpty(connection.Password))
                {
                    result.Skipped.Add(idArticle);
                    continue;
                }

                // Nome univoco della connessione: la matricola se c'è, altrimenti l'id.
                var article = await _db.Articles.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == idArticle, ct);
                var label = string.IsNullOrWhiteSpace(article?.SerialNumber)
                    ? $"Macchina {idArticle}"
                    : $"{article!.SerialNumber} (#{idArticle})";

                connections[label] = new
                {
                    protocol = "vnc",
                    parameters = new Dictionary<string, string>
                    {
                        ["hostname"] = connection.Address!,
                        ["port"] = connection.Port.ToString(),
                        ["password"] = connection.Password!,
                        ["disable-copy"] = "true",
                        ["disable-paste"] = "true",
                    },
                };
                result.Included.Add(idArticle);
            }

            if (connections.Count == 0)
                throw new InvalidOperationException("Nessuna macchina raggiungibile: accendere il pannello e aprire l'interfaccia BM.");

            // Un SOLO accesso Guacamole con tutte le connessioni: il tecnico le vede
            // insieme in una finestra sola, evitando il conflitto di sessione del
            // browser che lasciava vuota la seconda connessione.
            var payload = new
            {
                username = $"tech-{Guid.NewGuid():N}",
                expires = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds(),
                connections,
            };
            var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json));
            var token = GuacamoleToken.Encode(jsonBytes, _options.JsonSecretKey);
            result.Url = _options.GuacamoleUrl.TrimEnd('/') + "/?data=" + Uri.EscapeDataString(token);
            return result;
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Assistenza remota non configurata: manca la sezione RemoteSupport (provisioning e Guacamole).");
        }

        private async Task<Guid?> FindDeviceAsync(int idArticle, CancellationToken ct)
        {
            var row = await _db.MachineRemoteSupports.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdArticle == idArticle, ct);
            return row?.DeviceId;
        }

        private async Task<Guid> EnsureDeviceAsync(int idArticle, CancellationToken ct)
        {
            var row = await _db.MachineRemoteSupports.FirstOrDefaultAsync(x => x.IdArticle == idArticle, ct);
            if (row is not null)
                return row.DeviceId;

            row = new MachineRemoteSupport
            {
                IdArticle = idArticle,
                DeviceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
            };
            _db.MachineRemoteSupports.Add(row);
            await _db.SaveChangesAsync(ct);
            return row.DeviceId;
        }

        private async Task<T> AdminAsync<T>(string action, Guid deviceId, string actor, CancellationToken ct)
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                _options.ProvisioningUrl.TrimEnd('/') + "/admin/" + action);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ProvisioningApiKey);
            message.Content = JsonContent.Create(new { installationId = deviceId.ToString(), actor });

            using var client = _clients.CreateClient("RemoteSupport");
            using var response = await client.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                var reason = response.StatusCode == System.Net.HttpStatusCode.Conflict
                    ? "Dispositivo già associato: revocarlo prima di sostituirlo."
                    : "Il server di assistenza non ha completato l'operazione. Verificare collegamento e configurazione.";
                throw new InvalidOperationException(reason);
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new InvalidOperationException("Risposta del server di assistenza non valida.");
        }

        private sealed record IssueResponse(string Code, long ExpiresAt);
        private sealed record StatusResponse(string State, string? Address);
        private sealed record RevokeResponse(bool Revoked);
        private sealed record ConnectionResponse(string State, string? Address, string? Password, string? Protocol, int Port);
    }
}
