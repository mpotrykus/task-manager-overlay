using System.Windows.Media;
using TaskManagerOverlay.Models;

namespace TaskManagerOverlay.ViewModels;

public sealed class ProcessRowViewModel : ObservableObject
{
    public int Pid { get; }
    public string ProcessName { get; }

    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set => SetProperty(ref _icon, value); }

    private double _cpuPercent;
    public double CpuPercent { get => _cpuPercent; set => SetProperty(ref _cpuPercent, value); }

    private double _ramMb;
    public double RamMb { get => _ramMb; set => SetProperty(ref _ramMb, value); }

    private double? _gpuPercent;
    public double? GpuPercent { get => _gpuPercent; set => SetProperty(ref _gpuPercent, value); }

    private double _impactScore;
    public double ImpactScore { get => _impactScore; set => SetProperty(ref _impactScore, value); }

    private bool _isProtected;
    public bool IsProtected { get => _isProtected; set => SetProperty(ref _isProtected, value); }

    private bool _isSuspended;
    public bool IsSuspended { get => _isSuspended; set => SetProperty(ref _isSuspended, value); }

    public ProcessRowViewModel(ProcessSample sample)
    {
        Pid = sample.Pid;
        ProcessName = sample.ProcessName;
        UpdateFrom(sample);
    }

    public void UpdateFrom(ProcessSample sample)
    {
        Icon = sample.Icon;
        CpuPercent = sample.CpuPercent;
        RamMb = sample.RamMb;
        GpuPercent = sample.GpuPercent;
        ImpactScore = sample.ImpactScore;
        IsProtected = sample.IsProtected;
    }
}
