using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components.Gantt
{
    /// <summary>
    /// Pianificazione della commessa. Il grafico e' <see cref="RadzenGantt{TItem}"/>: geometria,
    /// assi, frecce di dipendenza, percorso critico e drag&amp;drop sono suoi. Qui restano il
    /// caricamento dati e l'editor della fase, che il componente non copre (dipendenze e ticket).
    /// </summary>
    public partial class RedGGantt : ComponentBase
    {
        [Parameter] public int CommessaId { get; set; }

        /// <summary>Notifica il contenitore che l'avanzamento del progetto potrebbe essere cambiato.</summary>
        [Parameter] public EventCallback OnProgressChanged { get; set; }

        [Inject] private ICommessaFaseService TaskService { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;

        private RadzenGantt<CommessaFaseDTO>? _gantt;

        private List<CommessaFaseDTO> _tasks = new();
        private List<GanttDependency<CommessaFaseDTO>> _dependencies = new();
        private bool _loading = true;
        private GanttZoomLevel _zoom = GanttZoomLevel.Week;

        /// <summary>Chiede un adattamento alla larghezza dopo il prossimo render con dati.</summary>
        private bool _fitPending;

        // Editor
        private CommessaFaseDTO? _editing;
        private List<CommessaFaseDTO> _parentOptions = new();
        private List<CommessaFaseDTO> _predecessorOptions = new();
        private int? _newPredecessorId;

        private record CompletionOption(string Text, CommessaFaseCompletionMode Value);
        private readonly List<CompletionOption> _completionOptions = new()
        {
            new("Tutti i ticket chiusi", CommessaFaseCompletionMode.AllTicketsClosed),
            new("Almeno un ticket chiuso", CommessaFaseCompletionMode.AnyTicketClosed),
            new("Manuale", CommessaFaseCompletionMode.Manual),
            new("Percentuale manuale", CommessaFaseCompletionMode.ProgressManual)
        };

        protected override async Task OnInitializedAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            _loading = true;
            _tasks = (await TaskService.GetTreeAsync(CommessaId)) ?? new();
            _tasks = _tasks.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();
            BuildDependencies();
            _loading = false;
            _fitPending = true;
            StateHasChanged();
        }

        /// <summary>
        /// Le dipendenze passano a Radzen come riferimenti agli oggetti task: e' la forma
        /// richiesta da ShowCriticalPath, che sulla variante per nomi di proprieta' non lavora.
        /// </summary>
        private void BuildDependencies()
        {
            var byId = _tasks.ToDictionary(t => t.Id);
            _dependencies = new List<GanttDependency<CommessaFaseDTO>>();

            foreach (var t in _tasks)
            {
                foreach (var d in t.Dependencies)
                {
                    if (!byId.TryGetValue(d.IdPredecessorFase, out var pred)) continue;
                    _dependencies.Add(new GanttDependency<CommessaFaseDTO>
                    {
                        From = pred,
                        To = t,
                        Type = MapDependencyType(d.Type)
                    });
                }
            }
        }

        private static GanttDependencyType MapDependencyType(DependencyType type) => type switch
        {
            DependencyType.StartToStart => GanttDependencyType.StartToStart,
            DependencyType.FinishToFinish => GanttDependencyType.FinishToFinish,
            DependencyType.StartToFinish => GanttDependencyType.StartToFinish,
            _ => GanttDependencyType.FinishToStart
        };

        private void SetZoom(GanttZoomLevel z) => _zoom = z;

        /// <summary>
        /// Adatta la scala alla larghezza disponibile: e' la funzione nativa che sostituisce
        /// il calcolo px-per-giorno che facevamo a mano. Invocata anche al primo caricamento.
        /// </summary>
        private void FitToWidth() => _gantt?.ZoomToFit();

        protected override void OnAfterRender(bool firstRender)
        {
            if (_fitPending && _gantt != null && _tasks.Any())
            {
                _fitPending = false;
                _gantt.ZoomToFit();
            }
        }

        // ─── Aspetto delle barre ──────────────────────────────────────────────
        private static string BarColorStyle(CommessaFaseDTO t)
            => (!t.ProgressFromTickets && !string.IsNullOrWhiteSpace(t.Color)) ? $"background:{t.Color};" : string.Empty;

        private static string BarStateClass(CommessaFaseDTO t)
        {
            if (t.HasBlockingTickets)
                return "blocked";
            if (t.Progress >= 100)
                return "done";
            if (t.TicketCount > 0 && t.OpenTicketCount == 0)
                return "done";
            if (t.EndDate.Date < DateTime.Today && t.Progress < 100)
                return "late";
            if (t.Progress > 0 || (t.TicketCount > 0 && t.ClosedTicketCount > 0))
                return "active";
            return "planned";
        }

        private static string TicketTitle(CommessaFaseDTO t)
            => t.HasBlockingTickets
                ? $"{t.BlockedTicketCount} ticket bloccati, {t.ClosedTicketCount} chiusi, {t.OpenTicketCount} aperti"
                : $"{t.ClosedTicketCount} ticket chiusi, {t.OpenTicketCount} aperti";

        private static string ProgressTitle(CommessaFaseDTO t)
            => t.ProgressFromTickets
                ? "Avanzamento calcolato dai ticket collegati"
                : "Avanzamento manuale";

        private static string BarTitle(CommessaFaseDTO t)
        {
            var source = t.ProgressFromTickets ? "da ticket" : "manuale";
            var tickets = t.TicketCount > 0 ? $" - ticket {t.ClosedTicketCount}/{t.TicketCount}" : string.Empty;
            var blocked = t.HasBlockingTickets ? $" - BLOCCATA ({t.BlockedTicketCount})" : string.Empty;
            return $"{t.Name} ({t.Progress}%, {source}){tickets}{blocked}";
        }

        private string TaskName(int id) => _tasks.FirstOrDefault(t => t.Id == id)?.Name ?? $"#{id}";

        // ─── Interazioni sul grafico ──────────────────────────────────────────
        private void OnTaskClick(CommessaFaseDTO task)
        {
            if (task != null) EditTask(task);
        }

        private async Task OnTaskMove(GanttTaskMovedEventArgs<CommessaFaseDTO> args) => await PersistDatesAsync(args);

        private async Task OnTaskResize(GanttTaskMovedEventArgs<CommessaFaseDTO> args) => await PersistDatesAsync(args);

        /// <summary>Radzen calcola le nuove date, a noi tocca solo salvarle.</summary>
        private async Task PersistDatesAsync(GanttTaskMovedEventArgs<CommessaFaseDTO> args)
        {
            var t = args.Data;
            if (t == null) return;

            t.StartDate = args.NewStart;
            t.EndDate = t.IsMilestone ? args.NewStart : args.NewEnd;

            await TaskService.BulkSaveAsync(new List<CommessaFaseDTO> { t });
            await LoadAsync();
            await OnProgressChanged.InvokeAsync();
        }

        // ─── Editor task ──────────────────────────────────────────────────────
        private void NewTask(bool milestone)
        {
            var start = DateTime.Today;
            _editing = new CommessaFaseDTO
            {
                IdCommessa = CommessaId,
                StartDate = start,
                EndDate = milestone ? start : start.AddDays(3),
                Progress = 0,
                IsMilestone = milestone,
                CompletionMode = CommessaFaseCompletionMode.AllTicketsClosed,
                AutoCreateTicketOnTake = !milestone,
                // Una fase creata a mano non ha un tipo ticket: pretendere un ticket per chiuderla
                // la renderebbe incompletabile. Si attiva esplicitamente dalla casella.
                RequiresTicket = false
            };
            BuildEditorOptions();
        }

        private void EditTask(CommessaFaseDTO t)
        {
            _editing = new CommessaFaseDTO
            {
                Id = t.Id,
                IdCommessa = t.IdCommessa,
                ParentId = t.ParentId,
                Name = t.Name,
                Description = t.Description,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                Progress = t.Progress,
                SortOrder = t.SortOrder,
                IsMilestone = t.IsMilestone,
                Color = t.Color,
                CompletionMode = t.CompletionMode,
                AutoCreateTicketOnTake = t.AutoCreateTicketOnTake,
                RequiresTicket = t.RequiresTicket,
                // Stato, gruppo e tipo ticket non si modificano da qui, ma vanno ricopiati: un DTO
                // parziale li riporterebbe ai valori di default (fase Pending, senza gruppo).
                State = t.State,
                IdGroup = t.IdGroup,
                IdTicketType = t.IdTicketType,
                Dependencies = t.Dependencies.Select(d => new CommessaFaseDependencyDTO
                {
                    Id = d.Id, IdFase = d.IdFase, IdPredecessorFase = d.IdPredecessorFase, LagDays = d.LagDays, Type = d.Type
                }).ToList()
            };
            BuildEditorOptions();
        }

        private void BuildEditorOptions()
        {
            _newPredecessorId = null;
            var selfId = _editing?.Id ?? 0;
            var descendants = selfId > 0 ? Descendants(selfId) : new HashSet<int>();
            descendants.Add(selfId);

            _parentOptions = _tasks.Where(t => !descendants.Contains(t.Id) && !t.IsMilestone).ToList();

            var existingPreds = _editing?.Dependencies.Select(d => d.IdPredecessorFase).ToHashSet() ?? new HashSet<int>();
            _predecessorOptions = _tasks
                .Where(t => t.Id != selfId && !existingPreds.Contains(t.Id) && !descendants.Contains(t.Id))
                .ToList();
        }

        private HashSet<int> Descendants(int id)
        {
            var result = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(id);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                foreach (var c in _tasks.Where(t => t.ParentId == cur))
                    if (result.Add(c.Id))
                        stack.Push(c.Id);
            }
            return result;
        }

        private void CancelEdit() => _editing = null;

        private async Task SaveTask()
        {
            if (_editing == null || string.IsNullOrWhiteSpace(_editing.Name)) return;
            var resp = await TaskService.SaveAsync(_editing);
            if (resp.State)
            {
                _editing = null;
                await LoadAsync();
                await OnProgressChanged.InvokeAsync();
            }
        }

        private async Task DeleteTask()
        {
            if (_editing == null || _editing.Id == 0) return;
            if (await DialogService.Confirm($"Eliminare il task \"{_editing.Name}\"?", "Conferma") != true) return;
            if (await TaskService.DeleteAsync(_editing.Id))
            {
                _editing = null;
                await LoadAsync();
                await OnProgressChanged.InvokeAsync();
            }
        }

        private async Task AddDependency()
        {
            if (_editing == null || _editing.Id == 0 || _newPredecessorId == null) return;
            var resp = await TaskService.AddDependencyAsync(new CommessaFaseDependencyDTO
            {
                IdFase = _editing.Id,
                IdPredecessorFase = _newPredecessorId.Value
            });
            if (resp.State)
            {
                await LoadAsync();
                RefreshEditingFromTasks();
            }
        }

        private async Task RemoveDependency(CommessaFaseDependencyDTO dep)
        {
            if (await TaskService.RemoveDependencyAsync(dep.Id))
            {
                await LoadAsync();
                RefreshEditingFromTasks();
            }
        }

        private void RefreshEditingFromTasks()
        {
            if (_editing == null) return;
            var fresh = _tasks.FirstOrDefault(t => t.Id == _editing.Id);
            if (fresh != null) EditTask(fresh);
        }
    }
}
