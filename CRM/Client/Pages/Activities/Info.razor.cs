using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Activities
{
    /// <summary>
    /// Scheda dell'attivita': i dati da una parte, le sue note spese dall'altra.
    /// <para>
    /// Il dialogo dell'agenda resta la via rapida per correggere data e assegnatario; questa
    /// pagina esiste perche' un'attivita' ha cose intorno - a partire dalle spese - che in una
    /// finestra non ci stanno, e perche' senza un indirizzo proprio non c'era modo di rimandare
    /// a una singola attivita' da un breadcrumb.
    /// </para>
    /// </summary>
    public partial class Info : ComponentBase
    {
        public enum ActivityViews
        {
            Dati,
            NoteSpese
        }

        [Inject] IActivityService ActivityService { get; set; }

        [Inject] IHeaderService HeaderService { get; set; }

        [Inject] NavigationManager Nav { get; set; }

        [Inject] DialogService DialogService { get; set; }

        [Inject] HttpClient Http { get; set; }

        [Inject] NotificationService Notification { get; set; }

        [Parameter] public int Id { get; set; }

        /// <summary>
        /// Scheda da aprire: "?view=notespese" ci porta dritto alle spese. Serve al ritorno dalle
        /// pagine figlie - salvata una nota spese si torna dove la si stava gestendo, non sui dati.
        /// </summary>
        [SupplyParameterFromQuery(Name = "view")]
        public string ViewParam { get; set; }

        private ActivityDTO _activity;
        private PageHeaderModel _pageHeader;
        private ActivityViews _view = ActivityViews.Dati;
        private bool _loading = true;
        private bool _completing;
        private int _expensesCount;

        /// <summary>Opportunita' e preventivi nati da questa visita.</summary>
        private ActivityOutcomeDTO _outcome;

        /// <summary>Valuta in cui si esprimono gli importi commerciali.</summary>
        private string _baseCurrency = "EUR";

        /// <summary>
        /// Si completa quello che e' ancora aperto, e solo se si ha il diritto di modificarlo:
        /// stessa condizione della riga in agenda, cosi' la scheda non offre un'azione che
        /// l'elenco nega (o viceversa).
        /// </summary>
        private bool CanComplete =>
            _activity != null
            && _activity.State == ActivityState.Planned
            && _activity.Permits.Edit();

        private string ExpensesTabText => _expensesCount > 0
            ? $"Note spese ({_expensesCount})"
            : "Note spese";

        private string StateText => _activity?.State switch
        {
            ActivityState.Done => "Completata",
            ActivityState.Cancelled => "Annullata",
            _ => "Pianificata"
        };

        private string StateBadgeClass => _activity?.State switch
        {
            ActivityState.Done => "bg-success",
            ActivityState.Cancelled => "bg-secondary",
            _ => "bg-primary"
        };

        private string EntityDescription =>
            string.IsNullOrWhiteSpace(_activity?.EntityName)
                ? $"{EntityTypeText} #{_activity?.EntityId}"
                : $"{EntityTypeText}: {_activity.EntityName}";

        private string EntityTypeText => _activity?.EntityType switch
        {
            ActivityEntityType.Company => "Azienda",
            ActivityEntityType.Contact => "Contatto",
            ActivityEntityType.Lead => "Lead",
            ActivityEntityType.Deal => "Trattativa",
            ActivityEntityType.Ticket => "Ticket",
            _ => "Collegamento"
        };

        /// <summary>
        /// Indirizzo dell'entita' collegata. Stessa mappa che l'agenda usa per il titolo della
        /// riga: se un giorno cambia una rotta, i due punti vanno allineati.
        /// </summary>
        private string EntityUrl => _activity == null ? null : _activity.EntityType switch
        {
            ActivityEntityType.Company => $"/Companies/{_activity.EntityId}",
            ActivityEntityType.Contact => $"/Contacts/{_activity.EntityId}",
            ActivityEntityType.Lead => $"/Leads/{_activity.EntityId}/Edit",
            ActivityEntityType.Deal => $"/Deals/{_activity.EntityId}",
            ActivityEntityType.Ticket => $"/Tickets/{_activity.EntityId}/Info",
            _ => null
        };

        protected override async Task OnParametersSetAsync()
        {
            if (string.Equals(ViewParam, "notespese", StringComparison.OrdinalIgnoreCase))
                _view = ActivityViews.NoteSpese;

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                _loading = true;
                _activity = await ActivityService.GetAsync(Id);
                _pageHeader = await HeaderService.Create();

                if (_pageHeader != null && _activity != null)
                    _pageHeader.Subtitle = _activity.Subject;

                await LoadExpensesCountAsync();
                await LoadOutcomeAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento attività: {ex.Message}");
                _activity = null;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// Numero di spese sulla linguetta: senza, per sapere se ce ne sono bisogna aprirla, e
        /// una scheda vuota e una non ancora guardata si somigliano troppo.
        /// </summary>
        private async Task LoadExpensesCountAsync()
        {
            try
            {
                var summary = await Http.GetFromJsonAsync<ExpenseReceiptSummaryDTO>(
                    $"api/ExpenseReceipts/activity/{Id}/summary");

                _expensesCount = summary?.TotalReceiptsCount ?? 0;
            }
            catch (Exception ex)
            {
                // Il conteggio e' un di piu': se non arriva, la linguetta resta senza numero.
                _expensesCount = 0;
                Console.Error.WriteLine($"Conteggio note spese non disponibile: {ex.Message}");
            }
        }

        /// <summary>
        /// Che cosa e' nato dalla visita. Se non arriva, il riquadro semplicemente non compare:
        /// e' una lettura in piu', non un dato senza il quale la scheda non ha senso.
        /// </summary>
        private async Task LoadOutcomeAsync()
        {
            try
            {
                _outcome = await Http.GetFromJsonAsync<ActivityOutcomeDTO>($"api/Activities/{Id}/outcome");
            }
            catch (Exception ex)
            {
                _outcome = null;
                Console.Error.WriteLine($"Esiti dell'attività non disponibili: {ex.Message}");
            }
        }

        /// <summary>
        /// Completamento con le stesse regole dell'agenda: per incontri, chiamate e incombenze si
        /// chiede prima esito e prossima azione, perche' e' li' che sta il valore di averle
        /// registrate; per una nota o un'email si chiude e basta.
        /// </summary>
        private async Task CompleteActivity()
        {
            if (!CanComplete)
                return;

            ActivityCompletionRequest completion = null;

            if (RequiresCompletionDetails(_activity.Kind))
            {
                completion = await DialogService.OpenAsync<ActivityCompleteDialog>(
                    "Completa attività",
                    new Dictionary<string, object>
                    {
                        ["Kind"] = _activity.Kind,
                        ["Subject"] = _activity.Subject,
                        ["EntityType"] = _activity.EntityType,
                        ["DefaultAssigneeId"] = _activity.IdAssignee
                    },
                    new DialogOptions
                    {
                        Width = "680px",
                        Height = "auto",
                        Resizable = false,
                        Draggable = true
                    }) as ActivityCompletionRequest;

                // Dialogo annullato: non si completa niente di nascosto.
                if (completion == null)
                    return;
            }

            try
            {
                _completing = true;

                var response = await ActivityService.CompleteAsync(Id, completion);

                if (!response.State)
                {
                    Notification.Notify(NotificationSeverity.Error, "Errore",
                        response.Message ?? "Operazione fallita");
                    return;
                }

                // Stessa regola dell'agenda: se dalla visita nasce qualcosa, si arriva al modulo
                // gia' compilato invece di doverlo cercare in un altro menu.
                var next = Agenda.FollowUpUrl(Id, completion);
                if (next != null)
                {
                    Nav.NavigateTo(next);
                    return;
                }

                await LoadAsync();
            }
            finally
            {
                _completing = false;
            }
        }

        private static bool RequiresCompletionDetails(ActivityKind kind)
            => kind is ActivityKind.Meeting or ActivityKind.Call or ActivityKind.Task;

        private async Task EditActivity()
        {
            var saved = await DialogService.OpenAsync<ActivityAgendaDialog>(
                "Modifica attività",
                new Dictionary<string, object> { ["ActivityId"] = Id },
                new DialogOptions
                {
                    Width = "920px",
                    Height = "auto",
                    Resizable = true,
                    Draggable = true
                });

            if (saved as bool? == true)
                await LoadAsync();
        }
    }
}
