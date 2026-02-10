using System.Timers;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VortexCut.Interop.Services;
using VortexCut.UI.Services;

namespace VortexCut.UI.ViewModels;

/// <summary>
/// 프리뷰 ViewModel
/// </summary>
public partial class PreviewViewModel : ViewModelBase, IDisposable
{
    private readonly ProjectService _projectService;
    private readonly System.Timers.Timer _playbackTimer;
    private bool _isPlaying = false;

    [ObservableProperty]
    private WriteableBitmap? _previewImage;

    [ObservableProperty]
    private long _currentTimeMs = 0;

    [ObservableProperty]
    private bool _isLoading = false;

    public PreviewViewModel(ProjectService projectService)
    {
        _projectService = projectService;

        // 30fps 재생 타이머
        _playbackTimer = new System.Timers.Timer(1000.0 / 30.0);
        _playbackTimer.Elapsed += OnPlaybackTick;
    }

    /// <summary>
    /// 특정 시간의 프레임 렌더링
    /// </summary>
    public async Task RenderFrameAsync(long timestampMs)
    {
        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                using var frame = _projectService.RenderFrame(timestampMs);
                if (frame != null)
                {
                    UpdatePreviewImage(frame);
                }
            });
            CurrentTimeMs = timestampMs;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// RenderedFrame을 WriteableBitmap으로 변환
    /// </summary>
    private void UpdatePreviewImage(RenderedFrame frame)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // RGBA 데이터를 WriteableBitmap으로 변환
            var bitmap = new WriteableBitmap(
                new Avalonia.PixelSize((int)frame.Width, (int)frame.Height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgba8888,
                Avalonia.Platform.AlphaFormat.Unpremul
            );

            using (var buffer = bitmap.Lock())
            {
                unsafe
                {
                    fixed (byte* srcPtr = frame.Data)
                    {
                        var dst = (byte*)buffer.Address;
                        var size = (int)frame.Width * (int)frame.Height * 4;
                        Buffer.MemoryCopy(srcPtr, dst, size, size);
                    }
                }
            }

            PreviewImage = bitmap;
        });
    }

    /// <summary>
    /// 재생/일시정지 토글
    /// </summary>
    public void TogglePlayback()
    {
        if (_isPlaying)
        {
            _playbackTimer.Stop();
            _isPlaying = false;
        }
        else
        {
            _playbackTimer.Start();
            _isPlaying = true;
        }
    }

    /// <summary>
    /// 재생 타이머 틱
    /// </summary>
    private void OnPlaybackTick(object? sender, ElapsedEventArgs e)
    {
        CurrentTimeMs += (long)(1000.0 / 30.0);
        _ = RenderFrameAsync(CurrentTimeMs);
    }

    /// <summary>
    /// 초기화
    /// </summary>
    public void Reset()
    {
        _playbackTimer.Stop();
        _isPlaying = false;
        CurrentTimeMs = 0;
        PreviewImage = null;
    }

    public void Dispose()
    {
        _playbackTimer?.Dispose();
    }

    [RelayCommand]
    private async Task SeekAsync(long timestampMs)
    {
        await RenderFrameAsync(timestampMs);
    }
}
