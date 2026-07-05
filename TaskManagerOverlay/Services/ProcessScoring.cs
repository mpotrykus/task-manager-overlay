namespace TaskManagerOverlay.Services;

/// <summary>
/// Pure scoring/exclusion logic, kept free of Process/WPF dependencies so it stays easy to reason about and test.
/// </summary>
public static class ProcessScoring
{
    public static bool IsExcludedFromMonitoring(int pid, string processName)
    {
        if (pid == 0 || pid == 4)
            return true;

        return AppConstants.MonitorExclusionDenylist.Contains(processName);
    }

    /// <summary>Weighted blend of CPU/GPU/RAM share, scaled to 0-100 (weights sum to 1.0) so it reads
    /// on the same scale as the percentages it's built from.</summary>
    public static double ComputeImpactScore(double cpuPercent, double? gpuPercent, double ramMb, double totalPhysicalRamMb)
    {
        double ramShare = totalPhysicalRamMb > 0 ? ramMb / totalPhysicalRamMb * 100.0 : 0;

        double score = cpuPercent / 100.0 * AppConstants.WeightCpu
                       + ramShare / 100.0 * AppConstants.WeightRam;

        if (gpuPercent is { } gpu)
            score += gpu / 100.0 * AppConstants.WeightGpu;

        return score * 100.0;
    }
}
