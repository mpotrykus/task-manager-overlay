using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using TaskManagerOverlay.Models;
using TaskManagerOverlay.Services;

namespace TaskManagerOverlay.ViewModels;

public sealed class OverlayViewModel : ObservableObject
{
    private readonly ProcessControlService _processControl;
    private readonly ListCollectionView _activeView;
    private readonly ListCollectionView _suspendedView;

    public ObservableCollection<ProcessRowViewModel> Rows { get; } = new();

    /// <summary>Non-suspended processes, sorted by impact score. This is the main scrollable list.</summary>
    public ICollectionView ActiveRowsView => _activeView;

    /// <summary>Suspended processes, pinned out of the impact-score sort so they don't get lost. Rendered as a small separate section.</summary>
    public ICollectionView SuspendedRowsView => _suspendedView;

    private ProcessRowViewModel? _selectedRow;
    public ProcessRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                CommandManager.InvalidateRequerySuggested();
                if (value?.Pid != _selectedProcessHistoryPid)
                    ResetSelectedProcessHistory(value?.Pid);
            }
        }
    }

    public bool IsGpuMonitoringAvailable { get; set; } = true;

    private double _cpuPercent;
    public double CpuPercent { get => _cpuPercent; set => SetProperty(ref _cpuPercent, value); }

    private double _ramUsedPercent;
    public double RamUsedPercent { get => _ramUsedPercent; set => SetProperty(ref _ramUsedPercent, value); }

    private string _ramSummaryText = string.Empty;
    public string RamSummaryText { get => _ramSummaryText; set => SetProperty(ref _ramSummaryText, value); }

    private double? _gpuPercent;
    public double? GpuPercent { get => _gpuPercent; set => SetProperty(ref _gpuPercent, value); }

    private int _processCount;
    public int ProcessCount { get => _processCount; set => SetProperty(ref _processCount, value); }

    private bool _isLoading = true;
    /// <summary>True until the first snapshot arrives from ProcessMonitorService, so the process
    /// list can show a loading indicator instead of a brief empty state on startup.</summary>
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    // Rolling trend history behind the overall-stats sparklines. Fixed 0-100 domain (all three
    // are percentages), so the tiles share one intuitive scale without needing a drawn axis.
    private const int SparklineHistoryLength = 30;
    private const double SparklineWidth = 108;
    private const double SparklineHeight = 26;

    private readonly List<double> _cpuHistory = new(SparklineHistoryLength);
    private readonly List<double> _ramHistory = new(SparklineHistoryLength);
    private readonly List<double> _gpuHistory = new(SparklineHistoryLength);

    private PointCollection _cpuHistoryPoints = new();
    public PointCollection CpuHistoryPoints { get => _cpuHistoryPoints; private set => SetProperty(ref _cpuHistoryPoints, value); }

    private PointCollection _cpuHistoryFillPoints = new();
    public PointCollection CpuHistoryFillPoints { get => _cpuHistoryFillPoints; private set => SetProperty(ref _cpuHistoryFillPoints, value); }

    private PointCollection _ramHistoryPoints = new();
    public PointCollection RamHistoryPoints { get => _ramHistoryPoints; private set => SetProperty(ref _ramHistoryPoints, value); }

    private PointCollection _ramHistoryFillPoints = new();
    public PointCollection RamHistoryFillPoints { get => _ramHistoryFillPoints; private set => SetProperty(ref _ramHistoryFillPoints, value); }

    private PointCollection? _gpuHistoryPoints;
    public PointCollection? GpuHistoryPoints { get => _gpuHistoryPoints; private set => SetProperty(ref _gpuHistoryPoints, value); }

    private PointCollection? _gpuHistoryFillPoints;
    public PointCollection? GpuHistoryFillPoints { get => _gpuHistoryFillPoints; private set => SetProperty(ref _gpuHistoryFillPoints, value); }

    // Rolling trend history behind the action modal's per-process sparklines. Only ever tracks
    // the single currently-selected process (not all rows), so the cost is one extra sample push
    // per tick regardless of how many hundred processes are in the list. Auto-scaled to the
    // buffer's own min/max rather than a fixed 0-100 domain, since RAM is in MB and most
    // processes sit at a tiny, otherwise-invisible sliver of CPU/GPU.
    private int? _selectedProcessHistoryPid;
    private readonly List<double> _selectedProcessCpuHistory = new(SparklineHistoryLength);
    private readonly List<double> _selectedProcessRamHistory = new(SparklineHistoryLength);
    private readonly List<double> _selectedProcessGpuHistory = new(SparklineHistoryLength);

    private PointCollection _selectedProcessCpuHistoryPoints = new();
    public PointCollection SelectedProcessCpuHistoryPoints { get => _selectedProcessCpuHistoryPoints; private set => SetProperty(ref _selectedProcessCpuHistoryPoints, value); }

    private PointCollection _selectedProcessCpuHistoryFillPoints = new();
    public PointCollection SelectedProcessCpuHistoryFillPoints { get => _selectedProcessCpuHistoryFillPoints; private set => SetProperty(ref _selectedProcessCpuHistoryFillPoints, value); }

    private PointCollection _selectedProcessRamHistoryPoints = new();
    public PointCollection SelectedProcessRamHistoryPoints { get => _selectedProcessRamHistoryPoints; private set => SetProperty(ref _selectedProcessRamHistoryPoints, value); }

    private PointCollection _selectedProcessRamHistoryFillPoints = new();
    public PointCollection SelectedProcessRamHistoryFillPoints { get => _selectedProcessRamHistoryFillPoints; private set => SetProperty(ref _selectedProcessRamHistoryFillPoints, value); }

    private PointCollection? _selectedProcessGpuHistoryPoints;
    public PointCollection? SelectedProcessGpuHistoryPoints { get => _selectedProcessGpuHistoryPoints; private set => SetProperty(ref _selectedProcessGpuHistoryPoints, value); }

    private PointCollection? _selectedProcessGpuHistoryFillPoints;
    public PointCollection? SelectedProcessGpuHistoryFillPoints { get => _selectedProcessGpuHistoryFillPoints; private set => SetProperty(ref _selectedProcessGpuHistoryFillPoints, value); }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _activeView.Refresh();
                _suspendedView.Refresh();
            }
        }
    }

    private ProcessSortMode _sortMode = ProcessSortMode.Cpu;
    public ProcessSortMode SortMode
    {
        get => _sortMode;
        private set
        {
            if (SetProperty(ref _sortMode, value))
            {
                OnPropertyChanged(nameof(IsSortedByCpu));
                OnPropertyChanged(nameof(IsSortedByRam));
                OnPropertyChanged(nameof(IsSortedByGpu));
                OnPropertyChanged(nameof(IsSortedByImpact));
            }
        }
    }

    public bool IsSortedByCpu => SortMode == ProcessSortMode.Cpu;
    public bool IsSortedByRam => SortMode == ProcessSortMode.Ram;
    public bool IsSortedByGpu => SortMode == ProcessSortMode.Gpu;
    public bool IsSortedByImpact => SortMode == ProcessSortMode.Impact;

    private bool _isActionModalOpen;
    /// <summary>Whether the per-process action modal (stats + Suspend/Kill/Cancel) is showing.</summary>
    public bool IsActionModalOpen { get => _isActionModalOpen; private set => SetProperty(ref _isActionModalOpen, value); }

    private ProcessRowViewModel? _actionModalRow;
    /// <summary>The process the action modal is showing/acting on - captured at open time so it stays
    /// stable even if the underlying selection changes while the modal is up.</summary>
    public ProcessRowViewModel? ActionModalRow { get => _actionModalRow; private set => SetProperty(ref _actionModalRow, value); }

    public ICommand OpenActionModalCommand { get; }
    public ICommand ConfirmSuspendResumeCommand { get; }
    public ICommand ConfirmKillCommand { get; }
    public ICommand CancelActionModalCommand { get; }
    public ICommand SetSortModeCommand { get; }

    public OverlayViewModel(ProcessControlService processControl)
    {
        _processControl = processControl;

        // Two independent views over the same Rows collection - constructing ListCollectionView
        // directly (rather than CollectionViewSource.GetDefaultView, which caches a single shared
        // view per source) is what lets the active and suspended lists filter/sort independently.
        _activeView = new ListCollectionView(Rows) { Filter = FilterActive };
        _activeView.IsLiveSorting = true;
        _activeView.LiveSortingProperties.Add(nameof(ProcessRowViewModel.CpuPercent));
        _activeView.LiveSortingProperties.Add(nameof(ProcessRowViewModel.RamMb));
        _activeView.LiveSortingProperties.Add(nameof(ProcessRowViewModel.GpuPercent));
        _activeView.LiveSortingProperties.Add(nameof(ProcessRowViewModel.ImpactScore));
        _activeView.IsLiveFiltering = true;
        _activeView.LiveFilteringProperties.Add(nameof(ProcessRowViewModel.IsSuspended));
        ApplySortMode();

        _suspendedView = new ListCollectionView(Rows) { Filter = FilterSuspended };
        _suspendedView.SortDescriptions.Add(new SortDescription(nameof(ProcessRowViewModel.ProcessName), ListSortDirection.Ascending));
        _suspendedView.IsLiveFiltering = true;
        _suspendedView.LiveFilteringProperties.Add(nameof(ProcessRowViewModel.IsSuspended));

        OpenActionModalCommand = new RelayCommand(OpenActionModal, () => SelectedRow is not null);
        ConfirmSuspendResumeCommand = new RelayCommand(ExecuteSuspendResume, () => ActionModalRow is { IsProtected: false });
        ConfirmKillCommand = new RelayCommand(ExecuteKill, () => ActionModalRow is { IsProtected: false });
        CancelActionModalCommand = new RelayCommand(CloseActionModal);
        SetSortModeCommand = new RelayCommand<ProcessSortMode>(mode => SetSortMode(mode));
    }

    private bool FilterActive(object obj) => obj is ProcessRowViewModel { IsSuspended: false } row && MatchesSearch(row);

    private bool FilterSuspended(object obj) => obj is ProcessRowViewModel { IsSuspended: true } row && MatchesSearch(row);

    private bool MatchesSearch(ProcessRowViewModel row)
        => string.IsNullOrWhiteSpace(_searchText) || row.ProcessName.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

    private void OpenActionModal()
    {
        if (SelectedRow is null)
            return;

        ActionModalRow = SelectedRow;
        IsActionModalOpen = true;
        CommandManager.InvalidateRequerySuggested();
    }

    private void CloseActionModal()
    {
        IsActionModalOpen = false;
        ActionModalRow = null;
    }

    private void ExecuteSuspendResume()
    {
        var row = ActionModalRow;
        if (row is null || row.IsProtected)
            return;

        bool success = row.IsSuspended
            ? _processControl.TryResume(row.Pid, row.ProcessName)
            : _processControl.TrySuspend(row.Pid, row.ProcessName);

        if (success)
            row.IsSuspended = !row.IsSuspended;

        CloseActionModal();
    }

    private void ExecuteKill()
    {
        var row = ActionModalRow;
        if (row is null || row.IsProtected)
            return;

        if (_processControl.TryKill(row.Pid, row.ProcessName))
            Rows.Remove(row);

        CloseActionModal();
    }

    /// <summary>The active list followed by the suspended list, so Up/Down navigation flows continuously across both sections.</summary>
    private List<ProcessRowViewModel> GetCombinedOrder()
        => _activeView.Cast<ProcessRowViewModel>().Concat(_suspendedView.Cast<ProcessRowViewModel>()).ToList();

    private static void PushHistory(List<double> history, double value)
    {
        history.Add(Math.Clamp(value, 0, 100));
        if (history.Count > SparklineHistoryLength)
            history.RemoveAt(0);
    }

    /// <summary>Maps a 0-100 history buffer onto a fixed logical canvas; the sparkline's Viewbox
    /// stretches that canvas to fit whatever pixel size the tile actually renders at.</summary>
    private static PointCollection BuildSparklinePoints(IReadOnlyList<double> history)
    {
        var points = new PointCollection();
        int count = history.Count;
        if (count == 0)
            return points;

        double stepX = count > 1 ? SparklineWidth / (count - 1) : 0;
        for (int i = 0; i < count; i++)
        {
            double x = count > 1 ? i * stepX : 0;
            double y = SparklineHeight - history[i] / 100.0 * SparklineHeight;
            points.Add(new Point(x, y));
        }

        if (count == 1)
            points.Add(new Point(SparklineWidth, points[0].Y));

        return points;
    }

    /// <summary>Closes the line points down to the baseline so they can double as a filled area polygon.</summary>
    private static PointCollection BuildFillPoints(PointCollection line)
    {
        if (line.Count == 0)
            return new PointCollection();

        var fill = new PointCollection(line)
        {
            new Point(line[^1].X, SparklineHeight),
            new Point(line[0].X, SparklineHeight)
        };
        return fill;
    }

    private static void PushRawHistory(List<double> history, double value)
    {
        history.Add(value);
        if (history.Count > SparklineHistoryLength)
            history.RemoveAt(0);
    }

    /// <summary>Same shape as BuildSparklinePoints, but scales to the buffer's own min/max instead of a
    /// fixed 0-100 domain - needed since RAM is in MB, and most processes sit at a sliver of CPU/GPU
    /// that a 0-100 scale would flatten to an invisible line.</summary>
    private static PointCollection BuildAutoScaledSparklinePoints(IReadOnlyList<double> history)
    {
        var points = new PointCollection();
        int count = history.Count;
        if (count == 0)
            return points;

        double min = history.Min();
        double range = history.Max() - min;

        double stepX = count > 1 ? SparklineWidth / (count - 1) : 0;
        for (int i = 0; i < count; i++)
        {
            double x = count > 1 ? i * stepX : 0;
            double normalized = range > 0.0001 ? (history[i] - min) / range : 0.5;
            double y = SparklineHeight - normalized * SparklineHeight;
            points.Add(new Point(x, y));
        }

        if (count == 1)
            points.Add(new Point(SparklineWidth, points[0].Y));

        return points;
    }

    /// <summary>Called whenever the selection changes to a different process (or to none), so the
    /// modal's sparklines never show a discontinuous jump from one process's data to another's.</summary>
    private void ResetSelectedProcessHistory(int? pid)
    {
        _selectedProcessHistoryPid = pid;
        _selectedProcessCpuHistory.Clear();
        _selectedProcessRamHistory.Clear();
        _selectedProcessGpuHistory.Clear();
        SelectedProcessCpuHistoryPoints = new PointCollection();
        SelectedProcessCpuHistoryFillPoints = new PointCollection();
        SelectedProcessRamHistoryPoints = new PointCollection();
        SelectedProcessRamHistoryFillPoints = new PointCollection();
        SelectedProcessGpuHistoryPoints = null;
        SelectedProcessGpuHistoryFillPoints = null;
    }

    /// <summary>Pushes this tick's sample for whichever process is currently selected into its rolling
    /// history. Must run after Rows have been updated from the latest snapshot.</summary>
    private void UpdateSelectedProcessHistory()
    {
        var row = SelectedRow;
        if (row is null)
            return;

        PushRawHistory(_selectedProcessCpuHistory, row.CpuPercent);
        SelectedProcessCpuHistoryPoints = BuildAutoScaledSparklinePoints(_selectedProcessCpuHistory);
        SelectedProcessCpuHistoryFillPoints = BuildFillPoints(SelectedProcessCpuHistoryPoints);

        PushRawHistory(_selectedProcessRamHistory, row.RamMb);
        SelectedProcessRamHistoryPoints = BuildAutoScaledSparklinePoints(_selectedProcessRamHistory);
        SelectedProcessRamHistoryFillPoints = BuildFillPoints(SelectedProcessRamHistoryPoints);

        if (row.GpuPercent is { } gpu)
        {
            PushRawHistory(_selectedProcessGpuHistory, gpu);
            SelectedProcessGpuHistoryPoints = BuildAutoScaledSparklinePoints(_selectedProcessGpuHistory);
            SelectedProcessGpuHistoryFillPoints = BuildFillPoints(SelectedProcessGpuHistoryPoints);
        }
        else
        {
            _selectedProcessGpuHistory.Clear();
            SelectedProcessGpuHistoryPoints = null;
            SelectedProcessGpuHistoryFillPoints = null;
        }
    }

    private static readonly ProcessSortMode[] SortModeCycle = { ProcessSortMode.Cpu, ProcessSortMode.Ram, ProcessSortMode.Gpu, ProcessSortMode.Impact };

    /// <summary>Cycles the active list's sort mode forward (+1) or backward (-1) through CPU/RAM/GPU/Impact, preserving the selected row.</summary>
    public void CycleSortMode(int direction)
    {
        int currentIndex = Array.IndexOf(SortModeCycle, SortMode);
        int nextIndex = ((currentIndex + direction) % SortModeCycle.Length + SortModeCycle.Length) % SortModeCycle.Length;
        SetSortMode(SortModeCycle[nextIndex]);
    }

    /// <summary>Switches the active list's sort mode directly (e.g. from clicking a column header), jumping selection back to the new top row.</summary>
    public void SetSortMode(ProcessSortMode mode)
    {
        SortMode = mode;
        ApplySortMode();
        SelectedRow = GetCombinedOrder().FirstOrDefault();
    }

    private void ApplySortMode()
    {
        string property = SortMode switch
        {
            ProcessSortMode.Cpu => nameof(ProcessRowViewModel.CpuPercent),
            ProcessSortMode.Ram => nameof(ProcessRowViewModel.RamMb),
            ProcessSortMode.Gpu => nameof(ProcessRowViewModel.GpuPercent),
            ProcessSortMode.Impact => nameof(ProcessRowViewModel.ImpactScore),
            _ => throw new ArgumentOutOfRangeException()
        };

        _activeView.SortDescriptions.Clear();
        _activeView.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Descending));
    }

    /// <summary>Moves the selection by one item within the combined active+suspended order. Must be called on the UI thread.</summary>
    public void MoveSelection(int delta)
    {
        var ordered = GetCombinedOrder();
        if (ordered.Count == 0)
        {
            SelectedRow = null;
            return;
        }

        int currentIndex = SelectedRow is null ? -1 : ordered.IndexOf(SelectedRow);
        int newIndex = Math.Clamp(currentIndex < 0 ? 0 : currentIndex + delta, 0, ordered.Count - 1);
        SelectedRow = ordered[newIndex];
    }

    /// <summary>
    /// Applies a fresh snapshot from ProcessMonitorService, updating existing rows in place
    /// (so the live views don't flicker) and preserving the selected PID across re-sorts.
    /// Must be called on the UI thread.
    /// </summary>
    public void ApplySnapshot(IReadOnlyList<ProcessSample> samples, SystemStatsSample stats)
    {
        IsLoading = false;

        CpuPercent = stats.CpuPercent;
        RamUsedPercent = stats.TotalRamMb > 0 ? stats.UsedRamMb / stats.TotalRamMb * 100.0 : 0;
        RamSummaryText = $"{stats.UsedRamMb / 1024.0:0.0} / {stats.TotalRamMb / 1024.0:0.0} GB";
        GpuPercent = stats.GpuPercent;
        ProcessCount = stats.ProcessCount;

        PushHistory(_cpuHistory, CpuPercent);
        CpuHistoryPoints = BuildSparklinePoints(_cpuHistory);
        CpuHistoryFillPoints = BuildFillPoints(CpuHistoryPoints);

        PushHistory(_ramHistory, RamUsedPercent);
        RamHistoryPoints = BuildSparklinePoints(_ramHistory);
        RamHistoryFillPoints = BuildFillPoints(RamHistoryPoints);

        if (stats.GpuPercent is { } gpuPercent)
        {
            PushHistory(_gpuHistory, gpuPercent);
            GpuHistoryPoints = BuildSparklinePoints(_gpuHistory);
            GpuHistoryFillPoints = BuildFillPoints(GpuHistoryPoints);
        }
        else
        {
            _gpuHistory.Clear();
            GpuHistoryPoints = null;
            GpuHistoryFillPoints = null;
        }

        int? selectedPid = SelectedRow?.Pid;
        int previousVisualIndex = SelectedRow is null ? -1 : GetCombinedOrder().IndexOf(SelectedRow);

        var byPid = Rows.ToDictionary(r => r.Pid);
        var samplePids = new HashSet<int>(samples.Select(s => s.Pid));

        foreach (var stale in Rows.Where(r => !samplePids.Contains(r.Pid)).ToList())
            Rows.Remove(stale);

        foreach (var sample in samples)
        {
            if (byPid.TryGetValue(sample.Pid, out var existingRow))
                existingRow.UpdateFrom(sample);
            else
                Rows.Add(new ProcessRowViewModel(sample));
        }

        var stillSelected = selectedPid is { } pid ? Rows.FirstOrDefault(r => r.Pid == pid) : null;
        if (stillSelected is not null)
        {
            SelectedRow = stillSelected;
        }
        else
        {
            var reordered = GetCombinedOrder();
            if (reordered.Count == 0)
            {
                SelectedRow = null;
            }
            else
            {
                int fallbackIndex = Math.Clamp(previousVisualIndex, 0, reordered.Count - 1);
                SelectedRow = reordered[fallbackIndex];
            }
        }

        UpdateSelectedProcessHistory();
    }
}
