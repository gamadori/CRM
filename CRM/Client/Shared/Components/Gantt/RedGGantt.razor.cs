using CRM.Client.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components.Gantt
{
    public partial class RedGGantt : ComponentBase, IAsyncDisposable
    {
        [Parameter] public int CommessaId { get; set; }

        /// <summary>Notifica il contenitore che l'avanzamento del progetto potrebbe essere cambiato.</summary>
        [Parameter] public EventCallback OnProgressChanged { get; set; }

        [Inject] private ICommessaFaseService TaskService { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;

        private enum Zoom { Day, Week, Month }

        // Layout
        private const int RowH = 34;
        private const int HeaderH = 44;

        /// <summary>
        /// Il suffisso di versione aggira la cache del service worker: senza, il browser puo'
        /// servire una copia vecchia del modulo. Va incrementato quando redg-gantt.js cambia.
        /// </summary>
        private const string ModulePath = "./js/redg-gantt.js?v=2";

        private List<CommessaFaseDTO> _tasks = new();
        private bool _loading = true;
        private Zoom _zoom = Zoom.Week;
        private double _pxPerDay = 10;
        private DateTime _min = DateTime.Today;
        private int _totalDays = 30;
        private int _todayX = -1;

        /// <summary>Spazio orizzontale disponibile per la timeline, misurato via JS (0 = non ancora noto).</summary>
        private double _availableWidth;

        /// <summary>Oltre questa densita' le barre diventano enormi senza aggiungere informazione
        /// (stesso tetto dell'anteprima template, per coerenza visiva tra i due Gantt).</summary>
        private const double MaxDayPx = 120;

        /// <summary>Limite di dilatazione rispetto allo zoom scelto, perche' resti significativo.</summary>
        private const double MaxZoomFactor = 6;

        private ElementReference _chartEl;
        private IJSObjectReference? _module;
        private IJSObjectReference? _interop;
        private DotNetObjectReference<RedGGantt>? _dotNetRef;

        // Editor
        private CommessaFaseDTO? _editing;
        private List<CommessaFaseDTO> _parentOptions = new();
        private List<CommessaFaseDTO> _predecessorOptions = new();
        private int? _newPredecessorId;

        // Derivati per il rendering
        private record AxisSeg(double X, double W, string Label);
        private record DayLine(double X, double W, bool Weekend);
        private record DepArc(string Points, bool Critical);

        private List<AxisSeg> _headerSegments = new();
        private List<DayLine> _dayLines = new();
        private List<DepArc> _depArcs = new();
        private Dictionary<int, int> _rowIndex = new();
        private Dictionary<int, int> _depthCache = new();

        private double ChartWidth => _totalDays * _pxPerDay;

        protected override async Task OnInitializedAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            _loading = true;
            _tasks = (await TaskService.GetTreeAsync(CommessaId)) ?? new();
            _tasks = _tasks.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();
            BuildLayout();
            _loading = false;
            StateHasChanged();
        }

        private static double PxFor(Zoom z) => z switch
        {
            Zoom.Day => 30,
            Zoom.Week => 11,
            Zoom.Month => 4.2,
            _ => 11
        };

        private void SetZoom(Zoom z)
        {
            _zoom = z;
            BuildLayout(); // la scala effettiva la ricalcola BuildLayout in base allo spazio
        }

        /// <summary>
        /// Scala orizzontale: parte dalla densita' dello zoom e si allarga fino a riempire lo
        /// spazio disponibile. Non scende mai sotto lo zoom scelto (sotto, la timeline scorre).
        /// </summary>
        private double ResponsivePxPerDay()
        {
            var basePx = PxFor(_zoom);
            if (_availableWidth <= 0 || _totalDays <= 0)
                return basePx;

            var max = Math.Max(basePx, Math.Min(MaxDayPx, basePx * MaxZoomFactor));
            var fill = _availableWidth / _totalDays;
            return Math.Clamp(fill, basePx, max);
        }

        /// <summary>Applica la larghezza misurata e ricostruisce il layout se e' cambiata.</summary>
        private void ApplyAvailableWidth(double width)
        {
            var usable = width - 16; // respiro a destra per l'ultima barra
            if (usable <= 0 || Math.Abs(usable - _availableWidth) < 1) return;

            _availableWidth = usable;
            BuildLayout();
            StateHasChanged();
        }

        [JSInvokable]
        public void OnContainerResize(double width) => ApplyAvailableWidth(width);

        private void BuildLayout()
        {
            _depthCache.Clear();
            _rowIndex = _tasks.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);

            if (_tasks.Count == 0)
            {
                _headerSegments = new();
                _dayLines = new();
                _depArcs = new();
                return;
            }

            var start = _tasks.Min(t => t.StartDate.Date);
            var end = _tasks.Max(t => (t.IsMilestone ? t.StartDate : t.EndDate).Date);
            _min = start.AddDays(-2);
            end = end.AddDays(2);
            _totalDays = Math.Max(7, (int)(end - _min).TotalDays + 1);

            // Dipende da _totalDays, quindi va dopo il calcolo dell'intervallo.
            _pxPerDay = ResponsivePxPerDay();

            var today = DateTime.Today;
            _todayX = (today >= _min && today <= _min.AddDays(_totalDays)) ? (int)((today - _min).TotalDays * _pxPerDay) : -1;

            BuildHeader();
            BuildDayLines();
            BuildDepArcs();
        }

        private void BuildHeader()
        {
            var segs = new List<AxisSeg>();
            if (_zoom == Zoom.Month)
            {
                var cur = new DateTime(_min.Year, _min.Month, 1);
                var last = _min.AddDays(_totalDays);
                while (cur <= last)
                {
                    var next = cur.AddMonths(1);
                    var segStart = cur < _min ? _min : cur;
                    var segEnd = next > last ? last : next;
                    double x = (segStart - _min).TotalDays * _pxPerDay;
                    double w = (segEnd - segStart).TotalDays * _pxPerDay;
                    segs.Add(new AxisSeg(x, w, cur.ToString("MMM yy", CultureInfo.CurrentCulture)));
                    cur = next;
                }
            }
            else if (_zoom == Zoom.Week)
            {
                // Settimane a partire dal lunedi'
                var cur = _min;
                int shift = ((int)cur.DayOfWeek + 6) % 7; // lunedi'=0
                cur = cur.AddDays(-shift);
                var last = _min.AddDays(_totalDays);
                while (cur <= last)
                {
                    double x = (cur - _min).TotalDays * _pxPerDay;
                    double w = 7 * _pxPerDay;
                    segs.Add(new AxisSeg(x, w, cur.ToString("dd/MM")));
                    cur = cur.AddDays(7);
                }
            }
            else // Day
            {
                for (int i = 0; i < _totalDays; i++)
                {
                    var d = _min.AddDays(i);
                    segs.Add(new AxisSeg(i * _pxPerDay, _pxPerDay, d.Day.ToString()));
                }
            }
            _headerSegments = segs;
        }

        private void BuildDayLines()
        {
            var lines = new List<DayLine>();
            if (_zoom != Zoom.Month)
            {
                for (int i = 0; i < _totalDays; i++)
                {
                    var d = _min.AddDays(i);
                    bool weekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                    lines.Add(new DayLine(i * _pxPerDay, _pxPerDay, weekend));
                }
            }
            else
            {
                var cur = new DateTime(_min.Year, _min.Month, 1).AddMonths(1);
                var last = _min.AddDays(_totalDays);
                while (cur <= last)
                {
                    lines.Add(new DayLine((cur - _min).TotalDays * _pxPerDay, 1, false));
                    cur = cur.AddMonths(1);
                }
            }
            _dayLines = lines;
        }

        private void BuildDepArcs()
        {
            var arcs = new List<DepArc>();
            foreach (var t in _tasks)
            {
                if (!_rowIndex.TryGetValue(t.Id, out var ti)) continue;
                foreach (var dep in t.Dependencies)
                {
                    var pred = _tasks.FirstOrDefault(x => x.Id == dep.IdPredecessorFase);
                    if (pred == null || !_rowIndex.TryGetValue(pred.Id, out var pi)) continue;

                    double x1 = pred.IsMilestone ? BarLeft(pred) + 4 : BarLeft(pred) + BarWidth(pred);
                    double y1 = pi * RowH + RowH / 2.0;
                    double x2 = BarLeft(t);
                    double y2 = ti * RowH + RowH / 2.0;
                    double mx = x1 + 10;

                    string pts = $"{F(x1)},{F(y1)} {F(mx)},{F(y1)} {F(mx)},{F(y2)} {F(x2 - 2)},{F(y2)}";
                    arcs.Add(new DepArc(pts, t.IsCriticalPath && pred.IsCriticalPath));
                }
            }
            _depArcs = arcs;
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // ─── Geometria barre ──────────────────────────────────────────────────
        private double BarLeft(CommessaFaseDTO t) => (t.StartDate.Date - _min).TotalDays * _pxPerDay;
        private double BarWidth(CommessaFaseDTO t) => Math.Max(_pxPerDay, Math.Max(1, t.DurationDays) * _pxPerDay);

        private string BarColorStyle(CommessaFaseDTO t)
            => (!t.IsCriticalPath && !t.ProgressFromTickets && !string.IsNullOrWhiteSpace(t.Color)) ? $"background:{t.Color};" : string.Empty;

        private static string BarStateClass(CommessaFaseDTO t)
        {
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
            => $"{t.ClosedTicketCount} ticket chiusi, {t.OpenTicketCount} aperti";

        private static string ProgressTitle(CommessaFaseDTO t)
            => t.ProgressFromTickets
                ? "Avanzamento calcolato dai ticket collegati"
                : "Avanzamento manuale";

        private static string BarTitle(CommessaFaseDTO t)
        {
            var source = t.ProgressFromTickets ? "da ticket" : "manuale";
            var tickets = t.TicketCount > 0 ? $" - ticket {t.ClosedTicketCount}/{t.TicketCount}" : string.Empty;
            return $"{t.Name} ({t.Progress}%, {source}){tickets}";
        }

        private int Depth(CommessaFaseDTO t)
        {
            if (_depthCache.TryGetValue(t.Id, out var d)) return d;
            int depth = 0;
            var cur = t;
            var guard = 0;
            while (cur?.ParentId != null && guard++ < 50)
            {
                depth++;
                cur = _tasks.FirstOrDefault(x => x.Id == cur.ParentId);
                if (cur == null) break;
            }
            _depthCache[t.Id] = depth;
            return depth;
        }

        private string TaskName(int id) => _tasks.FirstOrDefault(t => t.Id == id)?.Name ?? $"#{id}";

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
                IsMilestone = milestone
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

        // ─── Drag (callback dal modulo JS) ────────────────────────────────────
        [JSInvokable]
        public async Task OnBarDragEnd(int taskId, string role, int dayDelta)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == taskId);
            if (t == null || dayDelta == 0) return;

            if (t.IsMilestone || role == "move")
            {
                t.StartDate = t.StartDate.AddDays(dayDelta);
                t.EndDate = t.EndDate.AddDays(dayDelta);
            }
            else if (role == "resize-start")
            {
                var ns = t.StartDate.AddDays(dayDelta);
                if (ns <= t.EndDate) t.StartDate = ns;
            }
            else if (role == "resize-end")
            {
                var ne = t.EndDate.AddDays(dayDelta);
                if (ne >= t.StartDate) t.EndDate = ne;
            }

            await TaskService.BulkSaveAsync(new List<CommessaFaseDTO> { t });
            await LoadAsync();
            await OnProgressChanged.InvokeAsync();
        }

        [JSInvokable]
        public async Task OnLinkCreate(int fromTaskId, int toTaskId)
        {
            var resp = await TaskService.AddDependencyAsync(new CommessaFaseDependencyDTO
            {
                IdFase = toTaskId,
                IdPredecessorFase = fromTaskId
            });
            if (resp.State)
                await LoadAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // Inizializza il modulo JS quando il grafico e' presente nel DOM.
            if (_module == null && !_loading && _tasks.Any())
            {
                try
                {
                    _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
                    _dotNetRef = DotNetObjectReference.Create(this);
                    _interop = await _module.InvokeAsync<IJSObjectReference>("init", _chartEl, _dotNetRef);
                    // La prima misura arriva da sola: ResizeObserver notifica gia' all'observe().
                }
                catch (JSException)
                {
                    // Modulo assente o non aggiornato (es. copia vecchia nella cache del service
                    // worker): il Gantt resta leggibile, senza drag&drop e senza scala adattiva.
                    _module = null;
                    _interop = null;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_interop != null) { await _interop.InvokeVoidAsync("dispose"); await _interop.DisposeAsync(); }
                if (_module != null) await _module.DisposeAsync();
            }
            catch { /* circuito chiuso */ }
            _dotNetRef?.Dispose();
        }
    }
}
