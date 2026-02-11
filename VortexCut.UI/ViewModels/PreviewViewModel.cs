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

    [ObservableProperty]
    private bool _isPlaying = false;

    private TimelineViewModel? _timelineViewModel; // Timeline 참조

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
    /// TimelineViewModel 연결 (MainViewModel에서 호출)
    /// </summary>
    public void SetTimelineViewModel(TimelineViewModel timelineViewModel)
    {
        _timelineViewModel = timelineViewModel;
    }

    /// <summary>
    /// 특정 시간의 프레임 렌더링
    /// NOTE: Rust Renderer가 Mutex로 보호되므로 C# 측에서 동기화 불필요
    /// </summary>
    public async Task RenderFrameAsync(long timestampMs)
    {
        Services.DebugLogger.Log($"🖼️ RenderFrameAsync START: timestampMs={timestampMs}");
        IsLoading = true;
        try
        {
            // CRITICAL: frame 데이터를 먼저 복사해야 함 (using으로 인한 조기 해제 방지)
            byte[]? frameData = null;
            uint width = 0, height = 0;

            await Task.Run(() =>
            {
                Services.DebugLogger.Log($"   Calling _projectService.RenderFrame({timestampMs})...");
                using var frame = _projectService.RenderFrame(timestampMs);
                if (frame != null)
                {
                    Services.DebugLogger.Log($"   ✅ Frame rendered: {frame.Width}x{frame.Height}, Data size: {frame.Data.Length} bytes");
                    // 데이터를 복사 (frame이 dispose되기 전에)
                    frameData = frame.Data.ToArray();
                    width = frame.Width;
                    height = frame.Height;
                }
                else
                {
                    Services.DebugLogger.Log($"   ⚠️ Frame is null!");
                }
            });

            // UI 스레드에서 비트맵 업데이트
            if (frameData != null)
            {
                await UpdatePreviewImageAsync(frameData, width, height);
            }

            CurrentTimeMs = timestampMs;
            Services.DebugLogger.Log($"🖼️ RenderFrameAsync END: CurrentTimeMs={CurrentTimeMs}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 프레임 데이터를 WriteableBitmap으로 변환 (UI 스레드에서 실행)
    /// </summary>
    private async Task UpdatePreviewImageAsync(byte[] frameData, uint width, uint height)
    {
        Services.DebugLogger.Log($"   🔵 UpdatePreviewImageAsync: Creating bitmap {width}x{height}");

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Services.DebugLogger.Log($"      🔵 UI Thread executing!");
            // RGBA 데이터를 WriteableBitmap으로 변환
            var bitmap = new WriteableBitmap(
                new Avalonia.PixelSize((int)width, (int)height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgba8888,
                Avalonia.Platform.AlphaFormat.Unpremul
            );

            using (var buffer = bitmap.Lock())
            {
                unsafe
                {
                    fixed (byte* srcPtr = frameData)
                    {
                        var dst = (byte*)buffer.Address;
                        var size = (int)width * (int)height * 4;
                        Buffer.MemoryCopy(srcPtr, dst, size, size);
                    }
                }
            }

            Services.DebugLogger.Log($"      ✅ Bitmap created, setting PreviewImage property...");
            PreviewImage = bitmap;
            Services.DebugLogger.Log($"      ✅ PreviewImage set! PreviewImage is now {(PreviewImage != null ? "NOT NULL" : "NULL")}");
        });
    }

    /// <summary>
    /// 재생/일시정지 토글
    /// </summary>
    public void TogglePlayback()
    {
        System.Diagnostics.Debug.WriteLine($"▶️ TogglePlayback called! Current IsPlaying={IsPlaying}");

        if (IsPlaying)
        {
            System.Diagnostics.Debug.WriteLine("   ⏸️ Stopping playback...");
            _playbackTimer.Stop();
            IsPlaying = false;
        }
        else
        {
            // 클립이 없으면 재생하지 않음
            if (_timelineViewModel == null || _timelineViewModel.Clips.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("   ⚠️ No clips to play!");
                return;
            }

            System.Diagnostics.Debug.WriteLine("   ▶️ Starting playback...");
            // 재생 시작: Timeline의 현재 시간부터 시작
            if (_timelineViewModel != null)
            {
                CurrentTimeMs = _timelineViewModel.CurrentTimeMs;
                System.Diagnostics.Debug.WriteLine($"   Starting from CurrentTimeMs={CurrentTimeMs}");
            }
            _playbackTimer.Start();
            IsPlaying = true;
            System.Diagnostics.Debug.WriteLine("   ✅ Timer started!");
        }
    }

    /// <summary>
    /// 재생 타이머 틱 (INTERACTION_FLOWS.md: Latencyless Feel)
    /// </summary>
    private void OnPlaybackTick(object? sender, ElapsedEventArgs e)
    {
        // CRITICAL: Playhead 즉시 업데이트 (타이머를 블로킹하지 않음)
        CurrentTimeMs += (long)(1000.0 / 30.0);

        if (_timelineViewModel != null)
        {
            _timelineViewModel.CurrentTimeMs = CurrentTimeMs;
        }

        // Fire-and-forget: 렌더링은 백그라운드에서 (await 사용 안 함!)
        _ = Task.Run(async () =>
        {
            try
            {
                await RenderFrameAsync(CurrentTimeMs);
            }
            catch (Exception ex)
            {
                // 렌더링 에러는 로그만 남기고 재생 계속 (검은 프레임 표시)
                Services.DebugLogger.Log($"⚠️ Playback error (continuing): {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 초기화
    /// </summary>
    public void Reset()
    {
        _playbackTimer.Stop();
        IsPlaying = false;
        CurrentTimeMs = 0;
        PreviewImage = null;
    }

    public void Dispose()
    {
        _playbackTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private async Task SeekAsync(long timestampMs)
    {
        await RenderFrameAsync(timestampMs);
    }
}
