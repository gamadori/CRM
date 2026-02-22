using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class InterventionTimesManager : ComponentBase
    {
        [Inject]
        private HttpClient HttpClient { get; set; }

        [Inject]
        private DialogService DialogService { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int IdIntervention { get; set; }

        [Parameter]
        public List<TicketInterventionTimeModel> Times { get; set; } = new();

        [Parameter]
        public EventCallback<List<TicketInterventionTimeModel>> TimesChanged { get; set; }

        private RadzenDataGrid<TicketInterventionTimeModel> _timesGrid;
        private bool _isLoading = false;

        // Contatore per ID temporanei negativi (per elementi non ancora salvati)
        private int _tempIdCounter = -1;

        /// <summary>
        /// Indica se il componente lavora in modalità "in memoria" (nuovo intervento non ancora salvato)
        /// </summary>
        private bool IsInMemoryMode => IdIntervention <= 0;

        // Lista dei tipi di intervento per il dropdown
        private List<InterventionTimeType> _timeTypes = Enum.GetValues(typeof(InterventionTimeType)).Cast<InterventionTimeType>().ToList();

        // Proprietà calcolate per i riepiloghi
        private int TotalWorkMinutes => Times?.Where(t => t.TimeType == InterventionTimeType.Work).Sum(t => t.DurationMinutes) ?? 0;
        private int TotalTravelMinutes => Times?.Where(t => t.TimeType == InterventionTimeType.Travel).Sum(t => t.DurationMinutes) ?? 0;
        private int TotalBillableMinutes => Times?.Where(t => t.IsBillable).Sum(t => t.DurationMinutes) ?? 0;
        private int TotalKilometers => Times?.Where(t => t.TimeType == InterventionTimeType.Travel && t.TravelKilometers.HasValue).Sum(t => t.TravelKilometers!.Value) ?? 0;

        protected override async Task OnInitializedAsync()
        {
            if (!IsInMemoryMode)
            {
                await LoadTimes();
            }
        }

        private async Task LoadTimes()
        {
            try
            {
                _isLoading = true;
                var times = await HttpClient.GetFromJsonAsync<List<TicketInterventionTime>>($"api/TicketInterventionTime/{IdIntervention}");
                
                if (times != null)
                {
                    Times = times.Select(t => t.ToModel()).ToList();
                    await TimesChanged.InvokeAsync(Times);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento tempi: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Apre il dialog per aggiungere un nuovo periodo
        /// </summary>
        private async Task OpenAddDialog()
        {
            var newTime = new TicketInterventionTimeModel
            {
                IdTicketIntervention = IdIntervention,
                StartDateTime = DateTime.Now,
                EndDateTime = DateTime.Now.AddHours(1),
                TimeType = InterventionTimeType.Work,
                IsBillable = true
            };

            var result = await DialogService.OpenAsync<InterventionTimeDialog>(
                Localize["AddTimePeriod"],
                new Dictionary<string, object> 
                { 
                    { "Time", newTime },
                    { "IsNew", true }
                },
                new DialogOptions 
                { 
                    Width = "650px", 
                    Height = "auto",
                    Resizable = true,
                    Draggable = true,
                    CloseDialogOnEsc = true
                }
            );

            if (result is TicketInterventionTimeModel timeModel)
            {
                await _timesGrid.RefreshDataAsync(); //
                await CreateTime(timeModel);
            }
        }

        /// <summary>
        /// Apre il dialog per modificare un periodo esistente
        /// </summary>
        private async Task OpenEditDialog(TicketInterventionTimeModel item)
        {
            // Crea una copia per evitare modifiche dirette se l'utente annulla
            var editTime = new TicketInterventionTimeModel
            {
                Id = item.Id,
                IdTicketIntervention = item.IdTicketIntervention,
                StartDateTime = item.StartDateTime,
                EndDateTime = item.EndDateTime,
                TimeType = item.TimeType,
                Notes = item.Notes,
                IsBillable = item.IsBillable,
                TravelKilometers = item.TravelKilometers
            };

            var result = await DialogService.OpenAsync<InterventionTimeDialog>(
                "Modifica Periodo",
                new Dictionary<string, object> 
                { 
                    { "Time", editTime },
                    { "IsNew", false }
                },
                new DialogOptions 
                { 
                    Width = "650px", 
                    Height = "auto",
                    Resizable = true,
                    Draggable = true,
                    CloseDialogOnEsc = true
                }
            );

            if (result is TicketInterventionTimeModel timeModel)
            {
                await UpdateTime(timeModel);
            }
        }

        /// <summary>
        /// Crea un nuovo periodo di tempo
        /// </summary>
        private async Task CreateTime(TicketInterventionTimeModel item)
        {
            if (IsInMemoryMode)
            {
                // Modalità in memoria: aggiungi alla lista locale con ID temporaneo
                item.Id = _tempIdCounter--;
                item.IdTicketIntervention = 0;
                Times.Add(item);
                await TimesChanged.InvokeAsync(Times);
                StateHasChanged();
                return;
            }

            // Modalità persistente: salva su API
            try
            {
                _isLoading = true;

                var newTime = new TicketInterventionTime
                {
                    IdTicketIntervention = IdIntervention,
                    StartDateTime = item.StartDateTime,
                    EndDateTime = item.EndDateTime,
                    TimeType = item.TimeType,
                    Notes = item.Notes,
                    IsBillable = item.IsBillable,
                    TravelKilometers = item.TravelKilometers
                };

                Console.WriteLine($"[CreateTime] Invio richiesta POST per IdIntervention={IdIntervention}");
                var response = await HttpClient.PostAsJsonAsync("api/TicketInterventionTime", newTime);
                
                Console.WriteLine($"[CreateTime] Status Code: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var created = await response.Content.ReadFromJsonAsync<TicketInterventionTime>();
                    Console.WriteLine($"[CreateTime] Periodo creato con ID={created?.Id}");
                    
                    if (created != null)
                    {
                        item.Id = created.Id;
                        
                        // Ricarica manualmente i dati invece di usare grid.Reload()
                        Console.WriteLine($"[CreateTime] Ricaricamento dati...");
                        await LoadTimes();
                        Console.WriteLine($"[CreateTime] Completato!");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[CreateTime] ERRORE: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateTime] ECCEZIONE: {ex.GetType().Name}");
                Console.WriteLine($"[CreateTime] Message: {ex.Message}");
                Console.WriteLine($"[CreateTime] StackTrace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[CreateTime] InnerException: {ex.InnerException.Message}");
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Aggiorna un periodo di tempo esistente
        /// </summary>
        private async Task UpdateTime(TicketInterventionTimeModel item)
        {
            if (IsInMemoryMode)
            {
                // Modalità in memoria: aggiorna nella lista locale
                var existing = Times.FirstOrDefault(t => t.Id == item.Id);
                if (existing != null)
                {
                    existing.StartDateTime = item.StartDateTime;
                    existing.EndDateTime = item.EndDateTime;
                    existing.TimeType = item.TimeType;
                    existing.Notes = item.Notes;
                    existing.IsBillable = item.IsBillable;
                    existing.TravelKilometers = item.TravelKilometers;
                }
                await TimesChanged.InvokeAsync(Times);
                StateHasChanged();
                return;
            }

            // Modalità persistente: aggiorna su API
            try
            {
                _isLoading = true;

                var updateTime = new TicketInterventionTime
                {
                    Id = item.Id,
                    IdTicketIntervention = item.IdTicketIntervention,
                    StartDateTime = item.StartDateTime,
                    EndDateTime = item.EndDateTime,
                    TimeType = item.TimeType,
                    Notes = item.Notes,
                    IsBillable = item.IsBillable,
                    TravelKilometers = item.TravelKilometers
                };

                await HttpClient.PutAsJsonAsync($"api/TicketInterventionTime/{item.Id}", updateTime);
                
                // Ricarica manualmente i dati
                await LoadTimes();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore aggiornamento tempo: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task DeleteRow(TicketInterventionTimeModel item)
        {
            if (await DialogService.Confirm($"Eliminare il periodo dalle {item.StartDateTime:HH:mm} alle {item.EndDateTime:HH:mm}?") == true)
            {
                if (IsInMemoryMode)
                {
                    // Modalità in memoria: rimuovi dalla lista locale
                    Times.Remove(item);
                    await TimesChanged.InvokeAsync(Times);
                    StateHasChanged();
                    return;
                }

                // Modalità persistente: elimina da API
                try
                {
                    _isLoading = true;
                    await HttpClient.DeleteAsync($"api/TicketInterventionTime/{item.Id}");
                    
                    // Ricarica manualmente i dati
                    await LoadTimes();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore eliminazione tempo: {ex.Message}");
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        private void CalculateDuration(TicketInterventionTimeModel item)
        {
            // La durata viene calcolata automaticamente dalla proprietà DurationMinutes
            StateHasChanged();
        }

        private void OnTimeTypeChanged(TicketInterventionTimeModel item)
        {
            // Se non è un viaggio, rimuovi i km
            if (item.TimeType != InterventionTimeType.Travel)
            {
                item.TravelKilometers = null;
            }

            // Se è una pausa, non è fatturabile di default
            if (item.TimeType == InterventionTimeType.Break)
            {
                item.IsBillable = false;
            }

            StateHasChanged();
        }

        private string GetTimeTypeBadgeClass(InterventionTimeType timeType)
        {
            return timeType switch
            {
                InterventionTimeType.Work => "bg-work",
                InterventionTimeType.Travel => "bg-travel",
                InterventionTimeType.Break => "bg-break",
                _ => "bg-secondary"
            };
        }

        private string GetTimeTypeIcon(InterventionTimeType timeType)
        {
            return timeType switch
            {
                InterventionTimeType.Work => "work",
                InterventionTimeType.Travel => "directions_car",
                InterventionTimeType.Break => "coffee",
                _ => "schedule"
            };
        }

        private string FormatDuration(int minutes)
        {
            if (minutes == 0)
                return "0h 0m";

            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours}h {mins}m";
        }

        private string GetTimeTypeText(InterventionTimeType timeType)
        {
            // DEBUG: Log per verificare la traduzione
            var key = $"InterventionTimeType{timeType}";
            var translated = Localize[key];
            
            Console.WriteLine($"[GetTimeTypeText] Enum: {timeType}, Key: {key}, Translated: {translated}");
            
            // Se la traduzione non esiste o è uguale alla chiave, restituisce il valore dell'enum
            if (string.IsNullOrEmpty(translated) || translated == key)
            {
                // Fallback: traduzioni hard-coded temporanee
                return timeType switch
                {
                    InterventionTimeType.Work => "Lavoro",
                    InterventionTimeType.Travel => "Viaggio",
                    InterventionTimeType.Break => "Pausa",
                    _ => timeType.ToString()
                };
            }
            
            return translated;
        }
    }
}
