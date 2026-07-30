using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>
    /// Implementazione con cache di <see cref="ICurrentUserService"/>.
    /// Registrata come Scoped: in Blazor WebAssembly equivale alla durata dell'applicazione,
    /// quindi la copia sopravvive ai cambi di pagina — che e' il punto, visto che i permessi
    /// cambiano solo al login.
    /// </summary>
    public class CurrentUserService : ICurrentUserService, IDisposable
    {
        private readonly HttpClient _http;

        private readonly AuthenticationStateProvider _authenticationStateProvider;

        private readonly object _gate = new object();

        /// <summary>
        /// In cache va il Task, non il risultato: piu' componenti che si inizializzano insieme
        /// condividono la stessa richiesta invece di farne una a testa.
        /// </summary>
        private Task<ApplicationUser?>? _pending;

        public CurrentUserService(HttpClient http, AuthenticationStateProvider authenticationStateProvider)
        {
            _http = http;
            _authenticationStateProvider = authenticationStateProvider;

            // Cambio di identita' (logout, login di un altro utente): i permessi in cache
            // sono di qualcun altro e vanno buttati.
            _authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public async Task<ApplicationUser?> Get()
        {
            Task<ApplicationUser?> pending;

            lock (_gate)
            {
                _pending ??= Load();
                pending = _pending;
            }

            var user = await pending;

            // Un fallimento non resta in cache: senza questo un problema di rete momentaneo
            // lascerebbe la sessione senza permessi fino al ricaricamento della pagina.
            if (user == null)
                ClearIfCurrent(pending);

            return user;
        }

        public void Invalidate()
        {
            lock (_gate)
            {
                _pending = null;
            }
        }

        private async Task<ApplicationUser?> Load()
        {
            try
            {
                return await _http.GetFromJsonAsync<ApplicationUser>(ConstHelper.UserSignedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(CurrentUserService)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Azzera la cache solo se contiene ancora il tentativo fallito: se nel frattempo
        /// e' partito un caricamento nuovo, quello va lasciato dov'e'.
        /// </summary>
        private void ClearIfCurrent(Task<ApplicationUser?> pending)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_pending, pending))
                    _pending = null;
            }
        }

        private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => Invalidate();

        public void Dispose()
        {
            _authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
}
