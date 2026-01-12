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

        /// <summary>
        /// ✅ NUOVO: Estrae le iniziali dal nome completo dell'utente
        /// </summary>
        private string GetUserInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            else if (parts.Length == 1 && parts[0].Length >= 2)
            {
                return parts[0].Substring(0, 2).ToUpper();
            }
            else if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpper();
            }

            return "?";
        }

        /// <summary>
        /// ✅ NUOVO: Calcola il colore del testo in base alla luminosità dello sfondo
        /// Restituisce nero per sfondi chiari, bianco per sfondi scuri
        /// </summary>
        private string GetTextColorForBackground(string backgroundColor)
        {
            if (string.IsNullOrWhiteSpace(backgroundColor))
                return "#000000"; // Default nero

            try
            {
                // Rimuovi spazi e converti in lowercase
                var color = backgroundColor.Trim().ToLowerInvariant();

                int r, g, b;

                // Gestisci formato #RGB o #RRGGBB
                if (color.StartsWith("#"))
                {
                    color = color.Substring(1);
                    
                    if (color.Length == 3)
                    {
                        // Formato #RGB -> espandi a #RRGGBB
                        r = Convert.ToInt32(color.Substring(0, 1) + color.Substring(0, 1), 16);
                        g = Convert.ToInt32(color.Substring(1, 1) + color.Substring(1, 1), 16);
                        b = Convert.ToInt32(color.Substring(2, 1) + color.Substring(2, 1), 16);
                    }
                    else if (color.Length == 6)
                    {
                        // Formato #RRGGBB
                        r = Convert.ToInt32(color.Substring(0, 2), 16);
                        g = Convert.ToInt32(color.Substring(2, 2), 16);
                        b = Convert.ToInt32(color.Substring(4, 2), 16);
                    }
                    else
                    {
                        return "#000000"; // Formato non valido
                    }
                }
                // Gestisci formato rgb(r, g, b)
                else if (color.StartsWith("rgb(") && color.EndsWith(")"))
                {
                    var rgbValues = color.Substring(4, color.Length - 5).Split(',');
                    if (rgbValues.Length == 3)
                    {
                        r = int.Parse(rgbValues[0].Trim());
                        g = int.Parse(rgbValues[1].Trim());
                        b = int.Parse(rgbValues[2].Trim());
                    }
                    else
                    {
                        return "#000000"; // Formato non valido
                    }
                }
                // Gestisci colori nominali comuni
                else
                {
                    var namedColor = ParseNamedColor(color);
                    if (namedColor.HasValue)
                    {
                        r = namedColor.Value.r;
                        g = namedColor.Value.g;
                        b = namedColor.Value.b;
                    }
                    else
                    {
                        return "#000000"; // Colore non riconosciuto
                    }
                }

                // Calcola la luminosità relativa usando la formula WCAG
                // https://www.w3.org/TR/WCAG20/#relativeluminancedef
                double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

                // Se la luminosità è > 0.5, usa testo scuro, altrimenti chiaro
                return luminance > 0.5 ? "#000000" : "#FFFFFF";
            }
            catch
            {
                // In caso di errore, usa nero come default
                return "#000000";
            }
        }

        /// <summary>
        /// ✅ NUOVO: Parse colori nominali comuni
        /// </summary>
        private (int r, int g, int b)? ParseNamedColor(string colorName)
        {
            return colorName switch
            {
                "white" => (255, 255, 255),
                "black" => (0, 0, 0),
                "red" => (255, 0, 0),
                "green" => (0, 128, 0),
                "blue" => (0, 0, 255),
                "yellow" => (255, 255, 0),
                "orange" => (255, 165, 0),
                "purple" => (128, 0, 128),
                "pink" => (255, 192, 203),
                "gray" or "grey" => (128, 128, 128),
                "lightgray" or "lightgrey" => (211, 211, 211),
                "darkgray" or "darkgrey" => (169, 169, 169),
                _ => null
            };
        }
    }
}
