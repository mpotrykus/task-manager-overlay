namespace TaskManagerOverlay.Models;

/// <summary>System-wide totals sampled alongside the per-process list, for the overlay's "overall" summary strip.</summary>
public sealed record SystemStatsSample(
    double CpuPercent,
    double UsedRamMb,
    double TotalRamMb,
    double? GpuPercent,
    int ProcessCount);
