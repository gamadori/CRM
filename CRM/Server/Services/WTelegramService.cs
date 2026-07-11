using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using TL;

namespace CRM.Server.Services
{
    public sealed class WTelegramService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<WTelegramService> _logger;
        private WTelegram.Client? _client;

        public WTelegram.Client Client => _client ?? throw new InvalidOperationException($"Telegram non disponibile: {LastError ?? "client non inizializzato"}");
        public User? User => _client?.User;
        public string ConfigNeeded = "connecting";
        public string? LastError { get; private set; }
        public bool IsAvailable => _client is not null && LastError is null;

        public WTelegramService(IConfiguration config, ILogger<WTelegramService> logger)
        {
            _config = config;
            _logger = logger;
            WTelegram.Helpers.Log = (lvl, msg) => logger.Log((LogLevel)lvl, msg);
        }

        public override void Dispose()
        {
            _client?.Dispose();
            base.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                ConfigNeeded = await DoLogin(_config["phone_number"]);
            }
            catch (Exception ex)
            {
                DisableTelegram(ex);
            }
        }

        public async Task<string> DoLogin(string? loginInfo)
        {
            if (!EnsureClient())
            {
                return ConfigNeeded;
            }

            try
            {
                LastError = null;
                return ConfigNeeded = await Client.Login(loginInfo);
            }
            catch (Exception ex)
            {
                DisableTelegram(ex);
                return ConfigNeeded;
            }
        }

        public bool TryGetClient(out WTelegram.Client client)
        {
            if (EnsureClient())
            {
                client = Client;
                return true;
            }

            client = null!;
            return false;
        }

        private bool EnsureClient()
        {
            if (_client is not null)
            {
                return LastError is null;
            }

            try
            {
                LastError = null;
                _client = new WTelegram.Client(what => _config[what]);
                return true;
            }
            catch (IOException ex)
            {
                DisableTelegram(ex);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                DisableTelegram(ex);
                return false;
            }
        }

        private void DisableTelegram(Exception ex)
        {
            _client?.Dispose();
            _client = null;
            LastError = ex.Message;
            ConfigNeeded = "unavailable";
            _logger.LogWarning(ex, "Telegram non disponibile. Il CRM continua senza servizio Telegram.");
        }
    }
}
