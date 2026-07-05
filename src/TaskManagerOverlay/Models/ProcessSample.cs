using System.Windows.Media;

namespace TaskManagerOverlay.Models;

public sealed record ProcessSample(
    int Pid,
    string ProcessName,
    string? ExecutablePath,
    double CpuPercent,
    double RamMb,
    double? GpuPercent,
    double ImpactScore,
    bool IsProtected,
    ImageSource? Icon);
