using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{

    public class ProxyTicketInterventionUsersService : ProxyRestClientService<TicketInterventionUser, int, TicketInterventionUserFilter, object>, ITicketInterventionUsersService
    {
        public ProxyTicketInterventionUsersService(HttpClient http) : base(http, ConstHelper.TicketInterventionUsersPath)
        {

        }

        public async Task<HashSet<string>?> LoadAssignedUsers(int IdIntervention)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<List<string>>($"{_pathService}/intervention/{IdIntervention}/assigned-users");
                if (response != null)
                {
                    return new HashSet<string>(response);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti intervento: {ex.Message}");
                return null;
            }
        }

        public async Task<HttpResponseMessage> AssignUsersToIntervention(int IdIntervention, AssignUsersRequest Users)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/intervention/{IdIntervention}/assign-users", Users);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assegnazione utenti intervento: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

            }
        }
    }
}
