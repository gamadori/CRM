using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using QLNet;
using Radzen;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components.Gantt
{
    /// <summary>
    /// Pianificazione della commessa su scala a giorni lavorativi italiani. Le date nei DTO restano
    /// date reali; il grafico usa una mappa data -> indice lavorativo per comprimere weekend e festivi.
    /// La larghezza di un giorno non e' fissa: si ricava dallo spazio disponibile (misurato in JS con
    /// un ResizeObserver) cosi' che il piano riempia la fascia senza scroll inutile, e la scala si
    /// puo' comunque forzare con lo zoom.
    /// </summary>
    public partial class RedGGantt : ComponentBase, IAsyncDisposable
    {
        // Geometria verticale (fissa) e limiti di quella orizzontale (calcolata)
        private const int RowHeightPx = 34;
        private const int MonthBandPx = 20;
        private const int DayBandPx = 28;
        // +1: il bordo inferiore dell'asse, che va contato per tenere allineate le due teste
        private const int HeaderHeightPx = MonthBandPx + DayBandPx + 1;
        private const int BarHeightPx = 22;
        private const int SummaryBarPx = 10;
        private const int MilestonePx = 14;
        private const int MinDayPx = 8;
        private const int MaxDayPx = 96;
        private const int FallbackDayPx = 44;

        /// <summary>Moltiplicatori sulla larghezza "adatta": 1 = riempi lo spazio disponibile.</summary>
        private static readonly double[] ZoomSteps = { 0.4, 0.6, 0.8, 1, 1.4, 2, 3, 4.5 };
        private const int FitZoomIndex = 3;

        private static readonly QLNet.Calendar BusinessCalendar = new Italy(Italy.Market.Settlement);
        private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");

        [Parameter] public int CommessaId { get; set; }

        /// <summary>Notifica il contenitore che l'avanzamento del progetto potrebbe essere cambiato.</summary>
        [Parameter] public EventCallback OnProgressChanged { get; set; }

        [Inject] private ICommessaFaseService TaskService { get; set; } = default!;
        [Inject] private ITicketTypesService TicketTypeService { get; set; } = default!;
        [Inject] private IAGRestClientService Rest { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private List<CommessaFaseDTO> _tasks = new();
        private List<GanttRow> _rows = new();
        private List<DateTime> _workdays = new();
        private List<GanttArc> _arcs = new();
        private List<MonthBand> _months = new();
        private Dictionary<int, GanttRow> _rowById = new();
        private bool _loading = true;
        private DragState? _drag;
        private int _hoverRow = -1;

        // Scala orizzontale
        private ElementReference _gridEl;
        private IJSObjectReference? _module;
        private DotNetObjectReference<RedGGantt>? _selfRef;
        /// <summary>Identifica l'osservatore lato JS: sopravvive al rimontaggio della griglia.</summary>
        private readonly string _observerKey = Guid.NewGuid().ToString("N");
        /// <summary>Elemento attualmente osservato. Ogni ricarica ne crea uno nuovo, da riagganciare.</summary>
        private string? _attachedElementId;
        private int _viewportPx;
        private int _zoomIndex = FitZoomIndex;
        private int _dayPx = FallbackDayPx;

        // Editor
        private CommessaFaseDTO? _editing;
        private List<CommessaFaseDTO> _parentOptions = new();
        private List<CommessaFaseDTO> _predecessorOptions = new();
        private int? _newPredecessorId;
        private List<TicketType> _ticketTypes = new();
        private List<CRM.Shared.Group> _groups = new();

        private record CompletionOption(string Text, CommessaFaseCompletionMode Value);
        private readonly List<CompletionOption> _completionOptions = new()
        {
            new("Tutti i ticket chiusi", CommessaFaseCompletionMode.AllTicketsClosed),
            new("Almeno un ticket chiuso", CommessaFaseCompletionMode.AnyTicketClosed),
            new("Manuale", CommessaFaseCompletionMode.Manual),
            new("Percentuale manuale", CommessaFaseCompletionMode.ProgressManual)
        };

        private enum DragMode
        {
            Move,
            ResizeStart,
            ResizeEnd
        }

        private sealed record GanttRow(CommessaFaseDTO Task, int StartIndex, int EndIndex, int Workdays, int Depth, bool IsSummary);
        private sealed record GanttArc(int FromIndex, int ToIndex, int FromRow, int ToRow);
        private sealed record MonthBand(int StartIndex, int Days, string Label, string ShortLabel);
        private sealed record DragState(int TaskId, DragMode Mode, double StartClientX, int OriginalStartIndex, int OriginalEndIndex)
        {
            public int LastDelta { get; set; }
            public bool Moved { get; set; }
        }

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();
            await LoadEditorListsAsync();
        }

        /// <summary>
        /// Tipi ticket e gruppi per l'editor delle fasi. Caricati una volta sola: cambiano di rado
        /// e servono solo al pannello di modifica, che si apre dopo il primo rendering del piano.
        /// </summary>
        private async Task LoadEditorListsAsync()
        {
            var tipi = await TicketTypeService.GetList(new TicketTypeFilter { PageSize = 1000 });
            _ticketTypes = tipi?.Items ?? new();
            _groups = await Rest.Get<CRM.Shared.Group>(ConstHelper.GroupsPath) ?? new();
            StateHasChanged();
        }

        /// <summary>
        /// Ricarica il piano dal server. Serve al contenitore dopo un'operazione che cambia le date
        /// da fuori (riprogrammazione): il componente non ha modo di accorgersene da solo, e
        /// finche' resta montato continuerebbe a mostrare le fasi caricate all'inizializzazione.
        /// </summary>
        public async Task ReloadAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            _loading = true;
            _drag = null;
            _tasks = (await TaskService.GetTreeAsync(CommessaId)) ?? new();
            _tasks = _tasks.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();
            BuildTimeline();
            _loading = false;
            StateHasChanged();
        }

        // ─── Misura dello spazio disponibile ──────────────────────────────────
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // Il confronto e' sull'elemento, non su un flag: durante il caricamento la griglia
            // sparisce dal DOM e al ritorno e' un elemento nuovo, che va riosservato.
            if (_loading || _rows.Count == 0 || string.IsNullOrEmpty(_gridEl.Id) || _attachedElementId == _gridEl.Id)
                return;

            _attachedElementId = _gridEl.Id;
            _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./Shared/Components/Gantt/RedGGantt.razor.js");
            _selfRef ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("attach", _observerKey, _gridEl, _selfRef);
        }

        [JSInvokable]
        public void OnViewportResized(int width)
        {
            if (width <= 0 || width == _viewportPx)
                return;

            _viewportPx = width;
            BuildTimeline();
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_module != null)
                {
                    if (_attachedElementId != null)
                        await _module.InvokeVoidAsync("detach", _observerKey);
                    await _module.DisposeAsync();
                }
            }
            catch (JSDisconnectedException) { /* circuito gia' chiuso */ }
            catch (ObjectDisposedException) { /* modulo gia' rilasciato */ }

            _selfRef?.Dispose();
        }

        // ─── Costruzione della timeline ───────────────────────────────────────
        private void BuildTimeline()
        {
            _workdays = BuildWorkdays(_tasks);
            RecomputeScale();
            PadToViewport();
            _months = BuildMonthBands();

            var withChildren = _tasks.Where(t => t.ParentId.HasValue).Select(t => t.ParentId!.Value).ToHashSet();
            _rows = _tasks.Select(t => BuildRow(t, withChildren.Contains(t.Id))).ToList();
            _rowById = _rows.ToDictionary(r => r.Task.Id);
            _arcs = BuildArcs();
        }

        /// <summary>Pixel per giorno lavorativo: quota parte dello spazio disponibile, per lo zoom scelto.</summary>
        private void RecomputeScale()
        {
            if (_viewportPx <= 0)
            {
                _dayPx = FallbackDayPx;
                return;
            }

            var zoom = ZoomSteps[_zoomIndex];
            var max = (int)Math.Round(MaxDayPx * Math.Max(1, zoom));
            var fit = (double)_viewportPx / Math.Max(1, _workdays.Count) * zoom;
            // arrotondamento per difetto: un pixel di troppo per colonna fa comparire
            // una barra di scorrimento da 3px anche quando il piano ci starebbe tutto
            _dayPx = (int)Math.Floor(Math.Clamp(fit, MinDayPx, max));
        }

        /// <summary>
        /// Se il piano e' piu' corto della fascia visibile allunga il calendario attorno ad esso:
        /// meglio qualche giorno di contesto che mezza area vuota a destra.
        /// </summary>
        private void PadToViewport()
        {
            if (_viewportPx <= 0 || _dayPx <= 0)
                return;

            var append = true;
            while ((_workdays.Count + 1) * _dayPx <= _viewportPx && _workdays.Count < 500)
            {
                if (append)
                    _workdays.Add(NextOrSameWorkday(_workdays[^1].AddDays(1)));
                else
                    _workdays.Insert(0, PreviousOrSameWorkday(_workdays[0].AddDays(-1)));

                append = !append;
            }
        }

        private static List<DateTime> BuildWorkdays(IReadOnlyCollection<CommessaFaseDTO> tasks)
        {
            if (!tasks.Any())
                return new List<DateTime> { NextOrSameWorkday(DateTime.Today) };

            var min = tasks.Min(t => t.StartDate.Date);
            var max = tasks.Max(t => (t.IsMilestone ? t.StartDate : t.EndDate).Date);

            min = PreviousOrSameWorkday(min);
            max = NextOrSameWorkday(max < min ? min : max);

            var days = new List<DateTime>();
            for (var d = min; d <= max; d = d.AddDays(1))
            {
                if (IsWorkday(d))
                    days.Add(d);
            }

            if (days.Count == 0)
                days.Add(NextOrSameWorkday(min));

            return days;
        }

        /// <summary>Raggruppa i giorni lavorativi per mese: e' la banda superiore dell'asse.</summary>
        private List<MonthBand> BuildMonthBands()
        {
            var bands = new List<MonthBand>();
            var i = 0;
            while (i < _workdays.Count)
            {
                var month = _workdays[i];
                var count = 0;
                while (i + count < _workdays.Count
                       && _workdays[i + count].Month == month.Month
                       && _workdays[i + count].Year == month.Year)
                {
                    count++;
                }

                bands.Add(new MonthBand(
                    i,
                    count,
                    ItCulture.TextInfo.ToTitleCase(month.ToString("MMMM yyyy", ItCulture)),
                    ItCulture.TextInfo.ToTitleCase(month.ToString("MMM yy", ItCulture))));

                i += count;
            }

            return bands;
        }

        private GanttRow BuildRow(CommessaFaseDTO task, bool isSummary)
        {
            var start = IndexOfOrNextWorkday(task.StartDate.Date);
            var end = task.IsMilestone
                ? start
                : IndexOfOrPreviousWorkday(task.EndDate.Date);

            if (end < start)
                end = start;

            return new GanttRow(task, start, end, end - start + 1, DepthOf(task), isSummary && !task.IsMilestone);
        }

        private List<GanttArc> BuildArcs()
        {
            var arcs = new List<GanttArc>();
            for (var toRowIndex = 0; toRowIndex < _rows.Count; toRowIndex++)
            {
                var row = _rows[toRowIndex];
                foreach (var dependency in row.Task.Dependencies)
                {
                    if (!_rowById.TryGetValue(dependency.IdPredecessorFase, out var predecessor))
                        continue;

                    var fromRowIndex = _rows.IndexOf(predecessor);
                    var fromIndex = DependencyAnchor(predecessor, dependency.Type, from: true);
                    var toIndex = DependencyAnchor(row, dependency.Type, from: false);
                    arcs.Add(new GanttArc(fromIndex, toIndex, fromRowIndex, toRowIndex));
                }
            }

            return arcs;
        }

        private static int DependencyAnchor(GanttRow row, DependencyType type, bool from)
            => type switch
            {
                DependencyType.StartToStart => row.StartIndex,
                DependencyType.FinishToFinish => row.EndIndex,
                DependencyType.StartToFinish => from ? row.StartIndex : row.EndIndex,
                _ => from ? row.EndIndex : row.StartIndex
            };

        private int DepthOf(CommessaFaseDTO task)
        {
            var byId = _tasks.ToDictionary(t => t.Id);
            var seen = new HashSet<int> { task.Id };
            var depth = 0;
            var parentId = task.ParentId;

            while (parentId is int id && byId.TryGetValue(id, out var parent) && seen.Add(id))
            {
                depth++;
                parentId = parent.ParentId;
            }

            return Math.Min(depth, 6);
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

        private string BarTitle(GanttRow row)
        {
            var t = row.Task;
            var source = t.ProgressFromTickets ? "da ticket" : "manuale";
            var tickets = t.TicketCount > 0 ? $" - ticket {t.ClosedTicketCount}/{t.TicketCount}" : string.Empty;
            var blocked = t.HasBlockingTickets ? $" - BLOCCATA ({t.BlockedTicketCount})" : string.Empty;
            var range = t.IsMilestone
                ? t.StartDate.ToString("dd/MM/yyyy", ItCulture)
                : $"{t.StartDate:dd/MM/yyyy} - {t.EndDate:dd/MM/yyyy} ({row.Workdays} gg lav.)";
            return $"{t.Name} - {range} ({t.Progress}%, {source}){tickets}{blocked}";
        }

        private string TaskName(int id) => _tasks.FirstOrDefault(t => t.Id == id)?.Name ?? $"#{id}";

        // ─── Geometria della scala lavorativa ─────────────────────────────────
        private int TimelineWidthPx => Math.Max(_dayPx, _workdays.Count * _dayPx);
        private int TimelineHeightPx => Math.Max(RowHeightPx, _rows.Count * RowHeightPx);
        private string CanvasStyle => $"width:{TimelineWidthPx}px;--rg-day:{_dayPx}px;--rg-row:{RowHeightPx}px;";
        /// <summary>Altezza della testa di sinistra: deve pareggiare l'asse, che si dimensiona da solo.</summary>
        private string HeaderStyle => $"height:{HeaderHeightPx}px;";
        private string BodyStyle => $"height:{TimelineHeightPx}px;";
        private string RowHeightStyle => $"height:{RowHeightPx}px;";
        private bool IsFitZoom => _zoomIndex == FitZoomIndex;
        private bool IsMinZoom => _zoomIndex == 0;
        private bool IsMaxZoom => _zoomIndex == ZoomSteps.Length - 1;
        private string ZoomLabel => IsFitZoom ? "Adatta" : $"{ZoomSteps[_zoomIndex] * 100:0}%";

        private string FullDate(DateTime date) => date.ToString("dddd dd/MM/yyyy", ItCulture);

        private string ShortWeekday(DateTime date)
            => ItCulture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek).TrimEnd('.');

        private bool IsWeekStart(int index)
            => index > 0 && _workdays[index].DayOfWeek == DayOfWeek.Monday;

        /// <summary>Giorni di calendario saltati (weekend/festivi) prima della colonna indicata.</summary>
        private int SkippedBefore(int index)
            => index <= 0 ? 0 : (int)(_workdays[index] - _workdays[index - 1]).TotalDays - 1;

        private string GapTitle(int index)
        {
            var skipped = SkippedBefore(index);
            return skipped <= 0 ? string.Empty : $"{skipped} giorni non lavorativi compressi";
        }

        /// <summary>Con colonne strette il numero del giorno si stampa a salti, per non impastare l'asse.</summary>
        private int LabelEvery => _dayPx >= 26 ? 1 : (int)Math.Ceiling(26.0 / Math.Max(1, _dayPx));

        private bool ShowDayNumber(int index)
            => LabelEvery == 1 || index % LabelEvery == 0 || _workdays[index].Day == 1;

        private bool ShowWeekdayName => _dayPx >= 42;

        private string MonthBandStyle(MonthBand band)
            => $"left:{band.StartIndex * _dayPx}px;width:{band.Days * _dayPx}px;";

        private string MonthBandLabel(MonthBand band)
        {
            var width = band.Days * _dayPx;
            if (width < 38) return string.Empty;
            return width < 110 ? band.ShortLabel : band.Label;
        }

        private string DayStyle(int index) => $"left:{index * _dayPx}px;width:{_dayPx}px;";

        private string LineStyle(int index) => $"left:{index * _dayPx}px;";

        private string HoverStyle(int rowIndex) => $"top:{rowIndex * RowHeightPx}px;height:{RowHeightPx}px;";

        /// <summary>Posizione della linea di oggi; se oggi cade in un periodo compresso finisce sulla giunzione.</summary>
        private double? TodayOffsetPx
        {
            get
            {
                if (_workdays.Count == 0) return null;
                var today = DateTime.Today;
                if (today < _workdays[0] || today > _workdays[^1]) return null;

                var idx = _workdays.FindIndex(d => d >= today);
                if (idx < 0) return null;

                return _workdays[idx] == today
                    ? idx * _dayPx + (_dayPx / 2.0)
                    : idx * _dayPx;
            }
        }

        private string TodayLineStyle(double offset)
            => $"left:{offset.ToString("0.##", CultureInfo.InvariantCulture)}px;";

        private int BarInset => _dayPx >= 26 ? 3 : 1;

        private string BarStyle(GanttRow row)
        {
            var start = DisplayStartIndex(row);
            var end = DisplayEndIndex(row);
            var height = row.IsSummary ? SummaryBarPx : BarHeightPx;
            var left = start * _dayPx + BarInset;
            var width = Math.Max(6, (end - start + 1) * _dayPx - (BarInset * 2));
            var top = _rows.IndexOf(row) * RowHeightPx + ((RowHeightPx - height) / 2);
            return $"left:{left}px;top:{top}px;width:{width}px;height:{height}px;{BarColorStyle(row.Task)}";
        }

        /// <summary>
        /// Il nome sta dentro la barra solo se c'e' spazio; altrimenti va di fianco, e vicino al bordo
        /// destro si ribalta a sinistra per non finire fuori dall'area visibile.
        /// </summary>
        private string BarLabelClass(GanttRow row)
        {
            var start = DisplayStartIndex(row);
            var end = DisplayEndIndex(row);
            var width = (end - start + 1) * _dayPx;
            if (row.IsSummary) return "outside" + (EndsNearRightEdge(end) ? " flip" : string.Empty);
            if (width >= 68) return string.Empty;
            return "outside" + (EndsNearRightEdge(end) ? " flip" : string.Empty);
        }

        private bool EndsNearRightEdge(int endIndex)
            => _workdays.Count > 0 && (_workdays.Count - endIndex) * _dayPx < 190;

        private string MilestoneStyle(GanttRow row)
        {
            var left = DisplayStartIndex(row) * _dayPx + (_dayPx / 2.0) - (MilestonePx / 2.0);
            var top = _rows.IndexOf(row) * RowHeightPx + ((RowHeightPx - MilestonePx) / 2);
            return $"left:{left.ToString("0.##", CultureInfo.InvariantCulture)}px;top:{top}px;";
        }

        private string DependencyPath(GanttArc arc)
        {
            var fromX = arc.FromIndex * _dayPx + _dayPx - BarInset;
            var toX = arc.ToIndex * _dayPx + BarInset;
            var y1 = arc.FromRow * RowHeightPx + (RowHeightPx / 2);
            var y2 = arc.ToRow * RowHeightPx + (RowHeightPx / 2);

            if (Math.Abs(y1 - y2) < 1)
                return $"M {fromX} {y1} L {Math.Max(fromX + 10, toX)} {y2}";

            var middleX = arc.ToIndex >= arc.FromIndex
                ? Math.Max(fromX + 12, fromX + ((toX - fromX) / 2))
                : Math.Min(fromX - 12, fromX - 18);

            return $"M {fromX} {y1} L {middleX} {y1} L {middleX} {y2} L {toX} {y2}";
        }

        private int DisplayStartIndex(GanttRow row)
        {
            if (_drag?.TaskId != row.Task.Id)
                return row.StartIndex;

            return CalculateDragIndexes(row).Start;
        }

        private int DisplayEndIndex(GanttRow row)
        {
            if (_drag?.TaskId != row.Task.Id)
                return row.EndIndex;

            return CalculateDragIndexes(row).End;
        }

        private (int Start, int End) CalculateDragIndexes(GanttRow row)
        {
            if (_drag == null || _workdays.Count == 0)
                return (row.StartIndex, row.EndIndex);

            var max = _workdays.Count - 1;
            var start = _drag.OriginalStartIndex;
            var end = _drag.OriginalEndIndex;
            var delta = _drag.LastDelta;

            if (row.Task.IsMilestone)
            {
                start = Clamp(start + delta, 0, max);
                return (start, start);
            }

            switch (_drag.Mode)
            {
                case DragMode.Move:
                    var span = end - start;
                    start = Clamp(start + delta, 0, Math.Max(0, max - span));
                    end = start + span;
                    break;
                case DragMode.ResizeStart:
                    start = Clamp(start + delta, 0, end);
                    break;
                case DragMode.ResizeEnd:
                    end = Clamp(end + delta, start, max);
                    break;
            }

            return (start, end);
        }

        private static int Clamp(int value, int min, int max)
            => Math.Min(Math.Max(value, min), max);

        private int IndexOfOrNextWorkday(DateTime date)
        {
            var idx = _workdays.FindIndex(d => d >= date);
            return idx >= 0 ? idx : _workdays.Count - 1;
        }

        private int IndexOfOrPreviousWorkday(DateTime date)
        {
            for (var i = _workdays.Count - 1; i >= 0; i--)
            {
                if (_workdays[i] <= date)
                    return i;
            }

            return 0;
        }

        private static bool IsWorkday(DateTime date)
            => BusinessCalendar.isBusinessDay(new QLNet.Date(date.Day, date.Month, date.Year));

        private static DateTime NextOrSameWorkday(DateTime date)
        {
            var d = date.Date;
            while (!IsWorkday(d))
                d = d.AddDays(1);
            return d;
        }

        private static DateTime PreviousOrSameWorkday(DateTime date)
        {
            var d = date.Date;
            while (!IsWorkday(d))
                d = d.AddDays(-1);
            return d;
        }

        private static DateTime AddWorkdays(DateTime start, int workdays)
        {
            var d = start.Date;
            var remaining = Math.Max(0, workdays);
            while (remaining > 0)
            {
                d = NextOrSameWorkday(d.AddDays(1));
                remaining--;
            }

            return d;
        }

        // ─── Zoom e navigazione ───────────────────────────────────────────────
        private void Zoom(int direction)
        {
            var next = Clamp(_zoomIndex + direction, 0, ZoomSteps.Length - 1);
            if (next == _zoomIndex)
                return;

            _zoomIndex = next;
            BuildTimeline();
        }

        private void ResetZoom()
        {
            if (_zoomIndex == FitZoomIndex)
                return;

            _zoomIndex = FitZoomIndex;
            BuildTimeline();
        }

        private async Task ScrollToToday()
        {
            if (_module == null || TodayOffsetPx is not double offset)
                return;

            await _module.InvokeVoidAsync("scrollToPx", _gridEl, offset - (_viewportPx / 2.0));
        }

        // ─── Interazioni sul grafico ──────────────────────────────────────────
        private void OnRowEnter(int index) => _hoverRow = index;

        private void OnRowLeave() => _hoverRow = -1;

        private void OnTaskPointerDown(PointerEventArgs args, GanttRow row, DragMode mode)
        {
            if (args.Button != 0 || _workdays.Count == 0)
                return;

            _drag = new DragState(row.Task.Id, mode, args.ClientX, row.StartIndex, row.EndIndex);
        }

        private void OnChartPointerMove(PointerEventArgs args)
        {
            if (_drag == null)
                return;

            var delta = (int)Math.Round((args.ClientX - _drag.StartClientX) / _dayPx, MidpointRounding.AwayFromZero);
            if (delta == _drag.LastDelta)
                return;

            _drag.LastDelta = delta;
            _drag.Moved = true;
            StateHasChanged();
        }

        private async Task OnChartPointerUp(PointerEventArgs args)
        {
            if (_drag == null)
                return;

            var drag = _drag;
            _drag = null;

            var row = _rows.FirstOrDefault(r => r.Task.Id == drag.TaskId);
            if (row == null)
                return;

            if (!drag.Moved || drag.LastDelta == 0)
            {
                EditTask(row.Task);
                return;
            }

            var indexes = CalculateFinalDragIndexes(row, drag);
            var task = row.Task;
            task.StartDate = _workdays[indexes.Start];
            task.EndDate = task.IsMilestone ? task.StartDate : _workdays[indexes.End];

            await TaskService.BulkSaveAsync(new List<CommessaFaseDTO> { task });
            await LoadAsync();
            await OnProgressChanged.InvokeAsync();
        }

        private (int Start, int End) CalculateFinalDragIndexes(GanttRow row, DragState drag)
        {
            _drag = drag;
            var result = CalculateDragIndexes(row);
            _drag = null;
            return result;
        }

        // ─── Editor task ──────────────────────────────────────────────────────
        private void NewTask(bool milestone)
        {
            var start = NextOrSameWorkday(DateTime.Today);
            _editing = new CommessaFaseDTO
            {
                IdCommessa = CommessaId,
                StartDate = start,
                EndDate = milestone ? start : AddWorkdays(start, 3),
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
                // Lo stato non si modifica da qui ma va ricopiato: un DTO parziale riporterebbe la
                // fase a Pending. Gruppo e tipo ticket sono modificabili nel pannello sotto.
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
            if (_editing.IsMilestone || _editing.EndDate < _editing.StartDate)
                _editing.EndDate = _editing.StartDate;

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
