using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VortexCut.Core.Models;
using VortexCut.UI.Services;

namespace VortexCut.UI.ViewModels;

/// <summary>
/// 키프레임 시스템 타입
/// </summary>
public enum KeyframeSystemType
{
    Opacity,
    Volume,
    PositionX,
    PositionY,
    Scale,
    Rotation
}

/// <summary>
/// 타임라인 ViewModel
/// </summary>
public partial class TimelineViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private ulong _nextTrackId = 1;

    [ObservableProperty]
    private ObservableCollection<ClipModel> _clips = new();

    [ObservableProperty]
    private ObservableCollection<TrackModel> _videoTracks = new();

    [ObservableProperty]
    private ObservableCollection<TrackModel> _audioTracks = new();

    [ObservableProperty]
    private long _currentTimeMs = 0;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private ClipModel? _selectedClip;

    [ObservableProperty]
    private ObservableCollection<ClipModel> _selectedClips = new();

    [ObservableProperty]
    private ObservableCollection<MarkerModel> _markers = new();

    // Snap 설정
    [ObservableProperty]
    private bool _snapEnabled = true;

    [ObservableProperty]
    private long _snapThresholdMs = 100;

    // Razor 모드
    [ObservableProperty]
    private bool _razorModeEnabled = false;

    // 키프레임 시스템 선택
    [ObservableProperty]
    private KeyframeSystemType _selectedKeyframeSystem = KeyframeSystemType.Opacity;

    // Ripple 편집 모드
    [ObservableProperty]
    private bool _rippleModeEnabled = false;

    // In/Out 포인트 (워크에어리어)
    [ObservableProperty]
    private long? _inPointMs = null;

    [ObservableProperty]
    private long? _outPointMs = null;

    // 재생 중 여부
    [ObservableProperty]
    private bool _isPlaying = false;

    public RazorTool? RazorTool { get; private set; }
    public RippleEditService? RippleEditService { get; private set; }
    public LinkClipService? LinkClipService { get; private set; }

    public TimelineViewModel(ProjectService projectService)
    {
        _projectService = projectService;
        InitializeDefaultTracks();
        RazorTool = new RazorTool(this);
        RippleEditService = new RippleEditService(this);
        LinkClipService = new LinkClipService(this);
    }

    /// <summary>
    /// 기본 트랙 초기화 (8개 비디오 + 4개 오디오)
    /// </summary>
    private void InitializeDefaultTracks()
    {
        // 8개 비디오 트랙
        for (int i = 0; i < 8; i++)
        {
            AddVideoTrack();
        }

        // 4개 오디오 트랙
        for (int i = 0; i < 4; i++)
        {
            AddAudioTrack();
        }
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
    /// MediaItem으로부터 클립 추가 (드래그앤드롭용)
    /// </summary>
    public void AddClipFromMediaItem(MediaItem mediaItem, long startTimeMs, int trackIndex)
    {
        var clip = _projectService.AddVideoClip(mediaItem.FilePath, startTimeMs, mediaItem.DurationMs, trackIndex);
        Clips.Add(clip);
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
            if (RippleModeEnabled && RippleEditService != null)
            {
                // 리플 모드: 삭제 후 이후 클립 자동 이동
                RippleEditService.RippleDelete(SelectedClip);
            }
            else
            {
                // 일반 모드: 그냥 삭제
                Clips.Remove(SelectedClip);
            }
            SelectedClip = null;
        }
    }

    /// <summary>
    /// 비디오 트랙 추가
    /// </summary>
    [RelayCommand]
    public void AddVideoTrack()
    {
        // TODO: ProjectService.AddVideoTrack() 연동
        var track = new TrackModel
        {
            Id = _nextTrackId++,
            Index = VideoTracks.Count,
            Type = TrackType.Video,
            Name = $"V{VideoTracks.Count + 1}",
            ColorArgb = 0xFF0000FF // Blue
        };
        VideoTracks.Add(track);
    }

    /// <summary>
    /// 오디오 트랙 추가
    /// </summary>
    [RelayCommand]
    public void AddAudioTrack()
    {
        // TODO: ProjectService.AddAudioTrack() 연동
        var track = new TrackModel
        {
            Id = _nextTrackId++,
            Index = AudioTracks.Count,
            Type = TrackType.Audio,
            Name = $"A{AudioTracks.Count + 1}",
            ColorArgb = 0xFF00FF00 // Green
        };
        AudioTracks.Add(track);
    }

    /// <summary>
    /// 트랙 제거
    /// </summary>
    public void RemoveTrack(TrackModel track)
    {
        if (track.Type == TrackType.Video)
        {
            VideoTracks.Remove(track);
            // 인덱스 재정렬
            for (int i = 0; i < VideoTracks.Count; i++)
            {
                VideoTracks[i].Index = i;
            }
        }
        else
        {
            AudioTracks.Remove(track);
            for (int i = 0; i < AudioTracks.Count; i++)
            {
                AudioTracks[i].Index = i;
            }
        }
    }

    /// <summary>
    /// 마커 추가
    /// </summary>
    public void AddMarker(long timeMs, string name = "", MarkerType type = MarkerType.Comment)
    {
        var marker = new MarkerModel
        {
            Id = (ulong)(Markers.Count + 1),
            TimeMs = timeMs,
            Name = name,
            Type = type
        };
        Markers.Add(marker);
    }

    /// <summary>
    /// 현재 Playhead 위치에 마커 추가
    /// </summary>
    [RelayCommand]
    public void AddMarkerAtCurrentTime()
    {
        AddMarker(CurrentTimeMs, $"Marker {Markers.Count + 1}");
    }

    /// <summary>
    /// 마커 제거
    /// </summary>
    [RelayCommand]
    public void RemoveMarker(MarkerModel marker)
    {
        Markers.Remove(marker);
    }

    /// <summary>
    /// 클립에서 키프레임 시스템 가져오기
    /// </summary>
    private KeyframeSystem? GetKeyframeSystem(ClipModel clip, KeyframeSystemType type)
    {
        return type switch
        {
            KeyframeSystemType.Opacity => clip.OpacityKeyframes,
            KeyframeSystemType.Volume => clip.VolumeKeyframes,
            KeyframeSystemType.PositionX => clip.PositionXKeyframes,
            KeyframeSystemType.PositionY => clip.PositionYKeyframes,
            KeyframeSystemType.Scale => clip.ScaleKeyframes,
            KeyframeSystemType.Rotation => clip.RotationKeyframes,
            _ => null
        };
    }

    /// <summary>
    /// 현재 Playhead 위치에 키프레임 추가 (K 키)
    /// </summary>
    [RelayCommand]
    public void AddKeyframeAtCurrentTime()
    {
        if (SelectedClips.Count == 0) return;

        var clip = SelectedClips.First();
        var keyframeSystem = GetKeyframeSystem(clip, SelectedKeyframeSystem);
        if (keyframeSystem == null) return;

        // 클립 시작 기준 상대 시간 (초)
        double relativeTime = (CurrentTimeMs - clip.StartTimeMs) / 1000.0;
        if (relativeTime < 0 || relativeTime > clip.DurationMs / 1000.0)
            return; // 클립 범위 밖

        // 현재 보간된 값 사용 (키프레임이 있으면 보간, 없으면 50.0 기본값)
        double currentValue = keyframeSystem.Keyframes.Count > 0
            ? keyframeSystem.Interpolate(relativeTime)
            : 50.0;

        keyframeSystem.AddKeyframe(relativeTime, currentValue, InterpolationType.Linear);
    }

    /// <summary>
    /// In 포인트 설정 (I 키)
    /// </summary>
    [RelayCommand]
    public void SetInPoint(long timeMs)
    {
        InPointMs = timeMs;
    }

    /// <summary>
    /// Out 포인트 설정 (O 키)
    /// </summary>
    [RelayCommand]
    public void SetOutPoint(long timeMs)
    {
        OutPointMs = timeMs;
    }

    /// <summary>
    /// In/Out 포인트 지우기
    /// </summary>
    [RelayCommand]
    public void ClearInOutPoints()
    {
        InPointMs = null;
        OutPointMs = null;
    }

    /// <summary>
    /// 재생/일시정지 토글 (Space 키)
    /// </summary>
    [RelayCommand]
    public void TogglePlayback()
    {
        IsPlaying = !IsPlaying;
        // TODO: 실제 재생 로직 구현 (PreviewViewModel과 연동)
    }
}
