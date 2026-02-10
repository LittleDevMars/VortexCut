using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VortexCut.Core.Models;
using VortexCut.UI.Services;

namespace VortexCut.UI.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel - Kdenlive 스타일
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private IStorageProvider? _storageProvider;
    private bool _isInitialized = false;

    [ObservableProperty]
    private ProjectBinViewModel _projectBin;

    [ObservableProperty]
    private TimelineViewModel _timeline;

    [ObservableProperty]
    private PreviewViewModel _preview;

    [ObservableProperty]
    private string _projectName = "Untitled Project";

    public MainViewModel()
    {
        _projectService = new ProjectService();
        _projectBin = new ProjectBinViewModel();
        _timeline = new TimelineViewModel(_projectService);
        _preview = new PreviewViewModel(_projectService);
    }

    /// <summary>
    /// 초기화 (Window Opened에서 한 번만 호출)
    /// </summary>
    public void Initialize()
    {
        if (!_isInitialized)
        {
            CreateNewProject();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// StorageProvider 설정 (Window에서 호출)
    /// </summary>
    public void SetStorageProvider(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    [RelayCommand]
    private void CreateNewProject()
    {
        _projectService.CreateProject("New Project", 1920, 1080, 30.0);
        ProjectName = "New Project";
        Timeline.Reset();
        Preview.Reset();
        ProjectBin.Clear();
    }

    [RelayCommand]
    private async Task OpenVideoFileAsync()
    {
        if (_storageProvider == null)
            return;

        var fileTypes = new FilePickerFileType[]
        {
            new("Video Files")
            {
                Patterns = new[] { "*.mp4", "*.avi", "*.mkv", "*.mov", "*.wmv", "*.flv", "*.webm" }
            },
            new("All Files")
            {
                Patterns = new[] { "*.*" }
            }
        };

        var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "비디오 파일 가져오기",
            FileTypeFilter = fileTypes,
            AllowMultiple = true
        });

        int fileIndex = 0;
        foreach (var file in files)
        {
            var filePath = file.Path.LocalPath;
            var fileName = System.IO.Path.GetFileName(filePath);

            // Project Bin에 추가
            var mediaItem = new MediaItem
            {
                Name = fileName,
                FilePath = filePath,
                Type = MediaType.Video,
                DurationMs = 5000, // TODO: FFmpeg로 실제 duration 가져오기
                Width = 1920,
                Height = 1080,
                Fps = 30
            };

            ProjectBin.AddMediaItem(mediaItem);

            // 첫 번째 파일은 타임라인에도 자동 추가
            if (fileIndex == 0)
            {
                await Timeline.AddVideoClipAsync(filePath);
                await Preview.RenderFrameAsync(0);
            }

            fileIndex++;
        }
    }

    [RelayCommand]
    private void PlayPause()
    {
        Preview.TogglePlayback();
    }
}
