using System.Diagnostics;
using TaskManagerOverlay.Models;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Enumerates processes on a background thread, computes CPU%/RAM/GPU% and an impact score,
/// and raises a live-updating snapshot. All sampling/enumeration work happens off the UI
/// thread; subscribers are responsible for marshaling to the UI thread if needed.
/// </summary>
public sealed class ProcessMonitorService : IDisposable
{
    private readonly record struct CpuState(TimeSpan CpuTime, DateTime TimestampUtc);

    private readonly GpuUsageProvider _gpuUsageProvider = new();
    private readonly IconCacheService _iconCache = new();
    private readonly Thread _pollThread;
    private readonly int _selfPid = Environment.ProcessId;

    // System-wide totals, sourced from the same counters Task Manager itself reads. Both are
    // optional: some locked-down environments block the "Processor"/"Memory" categories, in
    // which case Tick() falls back to approximating from the per-process samples it already has.
    private readonly PerformanceCounter? _cpuTotalCounter;
    private readonly PerformanceCounter? _availableRamCounter;

    private Dictionary<int, CpuState> _prevCpuStates = new();
    private volatile bool _running = true;
    private volatile int _intervalMs = AppConstants.RefreshIntervalVisibleMs;

    public event Action<IReadOnlyList<ProcessSample>, SystemStatsSample>? Refreshed;

    public bool IsGpuMonitoringAvailable => _gpuUsageProvider.IsAvailable;

    public ProcessMonitorService()
    {
        _cpuTotalCounter = TryCreateCounter("Processor", "% Processor Time", "_Total");
        _availableRamCounter = TryCreateCounter("Memory", "Available MBytes", instanceName: null);

        _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "ProcessMonitorPoll" };
        _pollThread.Start();
    }

    private static PerformanceCounter? TryCreateCounter(string category, string counter, string? instanceName)
    {
        try
        {
            var pc = instanceName is null
                ? new PerformanceCounter(category, counter, readOnly: true)
                : new PerformanceCounter(category, counter, instanceName, readOnly: true);
            pc.NextValue(); // warm-up sample - the first read of a rate counter is meaningless
            return pc;
        }
        catch
        {
            return null;
        }
    }

    public void SetVisible(bool visible)
    {
        _intervalMs = visible ? AppConstants.RefreshIntervalVisibleMs : AppConstants.RefreshIntervalHiddenMs;
    }

    private void PollLoop()
    {
        while (_running)
        {
            try
            {
                Tick();
            }
            catch
            {
                // A single bad tick (e.g. transient enumeration failure) shouldn't kill the monitor loop.
            }

            Thread.Sleep(_intervalMs);
        }
    }

    private void Tick()
    {
        DateTime now = DateTime.UtcNow;
        double totalRamMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0;

        var currentPids = new HashSet<int>();
        var newCpuStates = new Dictionary<int, CpuState>();
        var raw = new List<(int Pid, string Name, string? Path, double CpuPercent, double RamMb)>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                int pid = process.Id;
                string name = process.ProcessName;
                currentPids.Add(pid);

                if (pid == _selfPid || ProcessScoring.IsExcludedFromMonitoring(pid, name))
                    continue;

                TimeSpan cpuTime;
                double ramMb;
                try
                {
                    cpuTime = process.TotalProcessorTime;
                    ramMb = process.WorkingSet64 / 1024.0 / 1024.0;
                }
                catch
                {
                    // Access denied (protected process) or exited between enumeration and query.
                    continue;
                }

                double cpuPercent = 0;
                if (_prevCpuStates.TryGetValue(pid, out var prev))
                {
                    double cpuDeltaTicks = (cpuTime - prev.CpuTime).Ticks;
                    double wallDeltaTicks = (now - prev.TimestampUtc).Ticks;
                    if (wallDeltaTicks > 0)
                        cpuPercent = Math.Clamp(cpuDeltaTicks / wallDeltaTicks / Environment.ProcessorCount * 100.0, 0, 100);
                }
                newCpuStates[pid] = new CpuState(cpuTime, now);

                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // No usable path (protected/other-bitness process) - row still shown with generic icon.
                }

                if (path is null)
                    continue; // Without a module path there's no meaningful name/icon/action target.

                raw.Add((pid, name, path, cpuPercent, ramMb));
            }
            finally
            {
                process.Dispose();
            }
        }

        _prevCpuStates = newCpuStates;
        _gpuUsageProvider.RefreshInstancesIfNeeded(currentPids);

        var samples = new List<ProcessSample>(raw.Count);
        double gpuPercentSum = 0;
        foreach (var r in raw)
        {
            double? gpuPercent = _gpuUsageProvider.GetGpuPercent(r.Pid);
            if (gpuPercent is { } gpu)
                gpuPercentSum += gpu;

            double impact = ProcessScoring.ComputeImpactScore(r.CpuPercent, gpuPercent, r.RamMb, totalRamMb);
            bool isProtected = AppConstants.ProtectedProcessDenylist.Contains(r.Name);
            var icon = _iconCache.GetIcon(r.Path);

            samples.Add(new ProcessSample(r.Pid, r.Name, r.Path, r.CpuPercent, r.RamMb, gpuPercent, impact, isProtected, icon));
        }

        samples.Sort((a, b) => b.ImpactScore.CompareTo(a.ImpactScore));

        var stats = new SystemStatsSample(
            CpuPercent: ReadCpuTotalPercent(raw),
            UsedRamMb: ReadUsedRamMb(totalRamMb, raw),
            TotalRamMb: totalRamMb,
            GpuPercent: _gpuUsageProvider.IsAvailable ? Math.Min(100.0, gpuPercentSum) : null,
            ProcessCount: samples.Count);

        Refreshed?.Invoke(samples, stats);
    }

    /// <summary>Prefers the real system-wide "_Total" counter; falls back to summing this tick's
    /// already-normalized per-process percentages if that counter is unavailable.</summary>
    private double ReadCpuTotalPercent(List<(int Pid, string Name, string? Path, double CpuPercent, double RamMb)> raw)
    {
        if (_cpuTotalCounter is not null)
        {
            try
            {
                return Math.Clamp(_cpuTotalCounter.NextValue(), 0, 100);
            }
            catch
            {
                // Counter instance vanished or category became unavailable - fall back below.
            }
        }

        return Math.Min(100.0, raw.Sum(r => r.CpuPercent));
    }

    /// <summary>Prefers the real "Available MBytes" counter (matches Task Manager); falls back to
    /// summing per-process working sets, which undercounts kernel/system memory but is close enough.</summary>
    private double ReadUsedRamMb(double totalRamMb, List<(int Pid, string Name, string? Path, double CpuPercent, double RamMb)> raw)
    {
        if (_availableRamCounter is not null)
        {
            try
            {
                return Math.Max(0, totalRamMb - _availableRamCounter.NextValue());
            }
            catch
            {
                // Counter became unavailable - fall back below.
            }
        }

        return raw.Sum(r => r.RamMb);
    }

    public void Dispose()
    {
        _running = false;
        _pollThread.Join(_intervalMs * 2);
        _gpuUsageProvider.Dispose();
        _cpuTotalCounter?.Dispose();
        _availableRamCounter?.Dispose();
    }
}
