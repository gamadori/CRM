using BlazoringComponents.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGTicketScheduler : ComponentBase
    {
        [Inject]
        private TooltipService tooltipService { get; set; }

        [CascadingParameter]
        public SchedulerTicket Ticket { get; set; }

        [Parameter]
        public Action<string> OpenModal { get; set; }

        private bool _showUsersPanel = false;

        void ShowTooltip(ElementReference elementReference, TooltipOptions options = null)
        {
            var tooltipContent = BuildTooltipContent();
            tooltipService.Open(elementReference, tooltipContent, options ?? new TooltipOptions()
            {
                Style = "background-color: yellow; color: black; max-width: 300px; max-height: 400px; overflow-y: auto; white-space: pre-wrap; word-wrap: break-word;",
                Duration = null,
                Position = TooltipPosition.Bottom
            });
        }

        void CloseTooltip() => tooltipService.Close();

        private string _styleTicket;

        protected override void OnInitialized()
        {

            base.OnInitialized();

        }
        protected override void OnParametersSet()
        {
            _styleTicket = $"background-color: {Ticket.BackGroundColor}; border-color: #313131; cursor: pointer";

            // ✅ Gestione fallback utenti legacy/multipli
            if (Ticket.AssignedUserNames == null || !Ticket.AssignedUserNames.Any())
            {
                if (string.IsNullOrEmpty(Ticket.User))
                {
                    Ticket.User = "Non Assegnato";
                }
            }

            base.OnInitialized();
        }

        /// <summary>
        /// ✅ Gestisce il click sulla card: chiude il pannello utenti (se aperto) e apre il modale
        /// </summary>
        private void HandleCardClick()
        {
            // Chiudi il pannello se è aperto
            if (_showUsersPanel)
            {
                _showUsersPanel = false;
            }
            
            // Apri sempre il modale
            OpenModal?.Invoke(Ticket.Id);
        }

        /// <summary>
        /// ✅ Mostra/nasconde il pannello con l'elenco completo degli utenti
        /// </summary>
        private void ToggleUsersPanel()
        {
            _showUsersPanel = !_showUsersPanel;
        }

        /// <summary>
        /// ✅ Costruisce il contenuto del tooltip (solo testo senza HTML)
        /// </summary>
        private string BuildTooltipContent()
        {
            var sb = new StringBuilder();
            
            // Descrizione ticket
            if (!string.IsNullOrEmpty(Ticket.Description))
            {
                sb.AppendLine($"Descrizione: {Ticket.Description}");
            }

            // Utenti assegnati
            if (Ticket.AssignedUserNames != null && Ticket.AssignedUserNames.Any())
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"Assegnato a ({Ticket.AssignedUserNames.Count}):");
                
                foreach (var userName in Ticket.AssignedUserNames)
                {
                    // Aggiungi stella al primo utente (principale)
                    if (Ticket.AssignedUserNames.IndexOf(userName) == 0)
                    {
                        sb.AppendLine($"  ★ {userName} (principale)");
                    }
                    else
                    {
                        sb.AppendLine($"  • {userName}");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(Ticket.User))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append($"Assegnato a: {Ticket.User}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// ✅ NUOVO: Restituisce testo per tooltip rapido (title attribute)
        /// </summary>
        private string GetAssignedUsersTooltip()
        {
            if (Ticket.AssignedUserNames == null || !Ticket.AssignedUserNames.Any())
                return "Nessun utente assegnato";

            return string.Join(", ", Ticket.AssignedUserNames);
        }
    }
}
