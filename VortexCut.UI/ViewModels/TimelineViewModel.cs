using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VortexCut.Core.Models;
using VortexCut.UI.Services;

namespace VortexCut.UI.ViewModels;

/// <summary>
/// 타임라인 ViewModel
/// </summary>
public partial class TimelineViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;

    [ObservableProperty]
    private ObservableCollection<ClipModel> _clips = new();

    [ObservableProperty]
    private long _currentTimeMs = 0;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private ClipModel? _selectedClip;

    public TimelineViewModel(ProjectService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>
    /// 비디오 파일 추가
    /// </summary>
    public async Task AddVideoClipAsync(string filePath)
    {
        await Task.Run(() =>
        {
            // TODO: 실제 비디오 길이 가져오기 (FFmpeg 메타데이터)
            long durationMs = 5000; // 임시로 5초
            var clip = _projectService.AddVideoClip(filePath, CurrentTimeMs, durationMs);

            // UI 스레드에서 실행
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Clips.Add(clip);
            });
        });
    }

    /// <summary>
    /// 타임라인 초기화
    /// </summary>
    public void Reset()
    {
        Clips.Clear();
        CurrentTimeMs = 0;
        SelectedClip = null;
    }

    [RelayCommand]
    private void SelectClip(ClipModel clip)
    {
        SelectedClip = clip;
    }

    [RelayCommand]
    private void DeleteSelectedClip()
    {
        if (SelectedClip != null)
        {
            Clips.Remove(SelectedClip);
            SelectedClip = null;
        }
    }
}
