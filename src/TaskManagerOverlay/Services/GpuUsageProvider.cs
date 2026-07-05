using System.Diagnostics;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Reads per-process GPU utilization from the Windows "GPU Engine" performance counter category.
/// Instance names look like "pid_1234_luid_..._engtype_3D"; a process can have multiple engine
/// instances (3D, Copy, VideoDecode, ...), so utilization is summed per PID and clamped to 100%.
/// </summary>
public sealed class GpuUsageProvider : IDisposable
{
    private const string CategoryName = "GPU Engine";
    private const string CounterName = "Utilization Percentage";

    private readonly Dictionary<string, PerformanceCounter> _counters = new(StringComparer.Ordinal);
    private Dictionary<int, List<string>> _pidToInstances = new();
    private HashSet<int> _lastKnownPids = new();
    private DateTime _lastRebuild = DateTime.MinValue;
    private bool _categoryAvailable;

    public GpuUsageProvider()
    {
        _categoryAvailable = TryCheckCategoryExists();
    }

    public bool IsAvailable => _categoryAvailable;

    /// <summary>
    /// Re-resolves the instance-name -> PID map when the running PID set has changed, or
    /// periodically as a fallback (engine instances churn independently of the PID set).
    /// GetInstanceNames() is the expensive call here, so this must not run every tick.
    /// </summary>
    public void RefreshInstancesIfNeeded(IReadOnlySet<int> currentPids)
    {
        if (!_categoryAvailable)
            return;

        bool pidsChanged = !currentPids.SetEquals(_lastKnownPids);
        bool timeElapsed = (DateTime.UtcNow - _lastRebuild).TotalMilliseconds >= AppConstants.GpuInstanceRebuildIntervalMs;

        if (!pidsChanged && !timeElapsed)
            return;

        _lastKnownPids = new HashSet<int>(currentPids);
        _lastRebuild = DateTime.UtcNow;
        RebuildInstanceMap();
    }

    public double? GetGpuPercent(int pid)
    {
        if (!_categoryAvailable)
            return null;

        if (!_pidToInstances.TryGetValue(pid, out var instances))
            return 0.0;

        double sum = 0;
        foreach (var name in instances)
        {
            if (!_counters.TryGetValue(name, out var counter))
                continue;

            try
            {
                sum += counter.NextValue();
            }
            catch (InvalidOperationException)
            {
                // Instance vanished between resolution and read; cleaned up on next rebuild.
            }
        }

        return Math.Min(100.0, sum);
    }

    private void RebuildInstanceMap()
    {
        try
        {
            var category = new PerformanceCounterCategory(CategoryName);
            string[] instanceNames = category.GetInstanceNames();

            var newMap = new Dictionary<int, List<string>>();
            var stillNeeded = new HashSet<string>(StringComparer.Ordinal);

            foreach (var name in instanceNames)
            {
                int? pid = ParsePid(name);
                if (pid is null)
                    continue;

                if (!newMap.TryGetValue(pid.Value, out var list))
                {
                    list = new List<string>();
                    newMap[pid.Value] = list;
                }
                list.Add(name);
                stillNeeded.Add(name);

                if (!_counters.ContainsKey(name))
                {
                    try
                    {
                        var counter = new PerformanceCounter(CategoryName, CounterName, name, readOnly: true);
                        counter.NextValue(); // warm-up sample; first read is not meaningful for a rate counter
                        _counters[name] = counter;
                    }
                    catch (InvalidOperationException)
                    {
                        // Instance disappeared between GetInstanceNames() and counter creation - skip it.
                    }
                }
            }

            foreach (var staleName in _counters.Keys.Where(name => !stillNeeded.Contains(name)).ToList())
            {
                _counters[staleName].Dispose();
                _counters.Remove(staleName);
            }

            _pidToInstances = newMap;
        }
        catch (Exception)
        {
            // Category disappeared or driver doesn't expose it reliably - degrade to "N/A" app-wide.
            _categoryAvailable = false;
        }
    }

    private static int? ParsePid(string instanceName)
    {
        const string marker = "pid_";
        int start = instanceName.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += marker.Length;
        int end = instanceName.IndexOf('_', start);
        if (end < 0)
            return null;

        return int.TryParse(instanceName.AsSpan(start, end - start), out int pid) ? pid : null;
    }

    private static bool TryCheckCategoryExists()
    {
        try
        {
            return PerformanceCounterCategory.Exists(CategoryName);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var counter in _counters.Values)
            counter.Dispose();
        _counters.Clear();
    }
}
