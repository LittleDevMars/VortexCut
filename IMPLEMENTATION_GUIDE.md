# VortexCut 구현 가이드 (Phase 3)

> **목적**: 이 문서는 AI 코딩 어시스턴트(Cursor 등)가 읽고 구현할 수 있도록 작성된 상세 설계서입니다.
> **작성일**: 2026-02-11
> **필수 선행 문서**: `docs/TECHSPEC.md`, `GUI_DESIGN_SPEC.md`
> **현재 상태**: Phase 2E 완료 (전문가급 타임라인), Phase 3 시작

---

## 아키텍처 개요

```
VortexCut.UI (C# Avalonia 11, CommunityToolkit.Mvvm 8.2.2)
  ├── ViewModels/  (MVVM ViewModel 계층)
  ├── Views/       (AXAML 뷰)
  ├── Controls/    (커스텀 컨트롤)
  └── Services/    (비즈니스 로직)
         ↓ FFI (P/Invoke, C ABI)
VortexCut.Interop (C# SafeHandle 래퍼)
         ↓
rust-engine (Rust, ffmpeg-next 8.0)
  ├── ffi/         (extern "C" 함수)
  ├── ffmpeg/      (디코더, 향후 인코더)
  ├── rendering/   (프레임 렌더러)
  └── timeline/    (타임라인 엔진)
```

---

## Phase 3A: 프로젝트 저장/불러오기

### 목표
편집 상태를 `.vortex` 파일(JSON)로 저장하고 복원할 수 있어야 한다.

### 기술 스택
- **직렬화**: `System.Text.Json` (NET 8 기본 제공, 추가 패키지 불필요)
- **파일 확장자**: `.vortex` (내부적으로 UTF-8 JSON)
- **파일 다이얼로그**: Avalonia `IStorageProvider`

### 저장 대상 데이터

```
ProjectData (최상위)
├── Version: string ("1.0")
├── ProjectName: string
├── Width: uint (1920)
├── Height: uint (1080)
├── Fps: double (30.0)
├── SnapEnabled: bool
├── SnapThresholdMs: long (100)
├── InPointMs: long?
├── OutPointMs: long?
│
├── MediaItems: MediaItemData[]
│   ├── Name: string
│   ├── FilePath: string
│   ├── Type: string ("Video"/"Audio"/"Image")
│   ├── DurationMs: long
│   ├── Width: uint
│   ├── Height: uint
│   └── Fps: double
│
├── VideoTracks: TrackData[]
│   ├── Id: ulong
│   ├── Index: int
│   ├── Name: string ("V1")
│   ├── IsMuted: bool
│   ├── IsSolo: bool
│   ├── IsLocked: bool
│   ├── ColorArgb: uint
│   └── Height: double
│
├── AudioTracks: TrackData[] (동일 구조)
│
├── Clips: ClipData[]
│   ├── Id: ulong
│   ├── FilePath: string
│   ├── StartTimeMs: long
│   ├── DurationMs: long
│   ├── TrackIndex: int
│   ├── ColorLabelArgb: uint
│   ├── LinkedAudioClipId: ulong?
│   ├── LinkedVideoClipId: ulong?
│   ├── OpacityKeyframes: KeyframeSystemData
│   ├── VolumeKeyframes: KeyframeSystemData
│   ├── PositionXKeyframes: KeyframeSystemData
│   ├── PositionYKeyframes: KeyframeSystemData
│   ├── ScaleKeyframes: KeyframeSystemData
│   └── RotationKeyframes: KeyframeSystemData
│
└── Markers: MarkerData[]
    ├── Id: ulong
    ├── TimeMs: long
    ├── Name: string
    ├── Comment: string
    ├── ColorArgb: uint
    ├── Type: string ("Comment"/"Chapter"/"Region")
    └── DurationMs: long?
```

**KeyframeSystemData 구조:**
```json
{
  "keyframes": [
    {
      "time": 0.0,
      "value": 100.0,
      "interpolation": "EaseIn",
      "inHandle": { "timeOffset": -0.1, "valueOffset": 10.0 },
      "outHandle": null
    }
  ]
}
```

### 저장하지 않는 것 (런타임 전용)
- CurrentTimeMs (재생 위치)
- ZoomLevel (줌 레벨)
- IsPlaying (재생 상태)
- SelectedClip (선택 상태)
- ThumbnailPath (캐시, 재생성 가능)

### 구현 파일 목록

#### 1. 새 파일: `VortexCut.Core/Serialization/ProjectSerializer.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VortexCut.Core.Serialization;

// JSON 직렬화용 DTO (Data Transfer Object)
public class ProjectData
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = "Untitled";

    [JsonPropertyName("width")]
    public uint Width { get; set; } = 1920;

    [JsonPropertyName("height")]
    public uint Height { get; set; } = 1080;

    [JsonPropertyName("fps")]
    public double Fps { get; set; } = 30.0;

    [JsonPropertyName("snapEnabled")]
    public bool SnapEnabled { get; set; } = true;

    [JsonPropertyName("snapThresholdMs")]
    public long SnapThresholdMs { get; set; } = 100;

    [JsonPropertyName("inPointMs")]
    public long? InPointMs { get; set; }

    [JsonPropertyName("outPointMs")]
    public long? OutPointMs { get; set; }

    [JsonPropertyName("mediaItems")]
    public List<MediaItemData> MediaItems { get; set; } = new();

    [JsonPropertyName("videoTracks")]
    public List<TrackData> VideoTracks { get; set; } = new();

    [JsonPropertyName("audioTracks")]
    public List<TrackData> AudioTracks { get; set; } = new();

    [JsonPropertyName("clips")]
    public List<ClipData> Clips { get; set; } = new();

    [JsonPropertyName("markers")]
    public List<MarkerData> Markers { get; set; } = new();
}

// TrackData, ClipData, MarkerData, KeyframeSystemData 등 DTO 정의
// ... (각 모델의 프로퍼티를 1:1 매핑)

public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(ProjectData project)
        => JsonSerializer.Serialize(project, Options);

    public static ProjectData? Deserialize(string json)
        => JsonSerializer.Deserialize<ProjectData>(json, Options);

    public static async Task SaveToFileAsync(ProjectData project, string filePath)
    {
        var json = Serialize(project);
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
    }

    public static async Task<ProjectData?> LoadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        return Deserialize(json);
    }
}
```

#### 2. 수정 파일: `VortexCut.UI/Services/ProjectService.cs`

추가할 메서드:
```csharp
// ViewModel → DTO 변환
public ProjectData ExtractProjectData(MainViewModel mainVm)
{
    var data = new ProjectData
    {
        ProjectName = mainVm.ProjectName,
        Width = mainVm.Width,
        Height = mainVm.Height,
        Fps = mainVm.Fps,
        // ...
    };

    // TimelineViewModel → ClipData[], TrackData[], MarkerData[]
    foreach (var clip in mainVm.Timeline.Clips)
        data.Clips.Add(ClipToDto(clip));

    foreach (var track in mainVm.Timeline.VideoTracks)
        data.VideoTracks.Add(TrackToDto(track));

    // ... Markers, AudioTracks, MediaItems
    return data;
}

// DTO → ViewModel 복원
public void RestoreProjectData(ProjectData data, MainViewModel mainVm)
{
    mainVm.ProjectName = data.ProjectName;
    // Rust Timeline 재생성
    _timelineService.DestroyTimeline();
    _timelineService.CreateTimeline(data.Width, data.Height, data.Fps);

    // 트랙 복원
    foreach (var track in data.VideoTracks)
    {
        var trackId = _timelineService.AddVideoTrack();
        mainVm.Timeline.VideoTracks.Add(DtoToTrack(track));
    }

    // 클립 복원 (Rust + C# 동시)
    foreach (var clip in data.Clips)
    {
        _timelineService.AddVideoClip(clip.TrackId, clip.FilePath, clip.StartTimeMs, clip.DurationMs);
        mainVm.Timeline.Clips.Add(DtoToClip(clip));
    }

    // 마커 복원 (C#만)
    foreach (var marker in data.Markers)
        mainVm.Timeline.Markers.Add(DtoToMarker(marker));
}
```

#### 3. 수정 파일: `VortexCut.UI/ViewModels/MainViewModel.cs`

추가할 Commands:
```csharp
[RelayCommand]
private async Task SaveProject()
{
    var data = _projectService.ExtractProjectData(this);
    // Avalonia IStorageProvider로 파일 다이얼로그
    var filePath = await ShowSaveDialog(".vortex");
    if (filePath != null)
        await ProjectSerializer.SaveToFileAsync(data, filePath);
}

[RelayCommand]
private async Task LoadProject()
{
    var filePath = await ShowOpenDialog(".vortex");
    if (filePath == null) return;

    var data = await ProjectSerializer.LoadFromFileAsync(filePath);
    if (data != null)
        _projectService.RestoreProjectData(data, this);
}
```

#### 4. 수정 파일: `VortexCut.UI/Views/MainWindow.axaml`

메뉴에 추가:
```xml
<MenuItem Header="File">
    <MenuItem Header="Save Project" Command="{Binding SaveProjectCommand}" InputGesture="Ctrl+S"/>
    <MenuItem Header="Open Project" Command="{Binding LoadProjectCommand}" InputGesture="Ctrl+O"/>
    <MenuItem Header="Save As..." Command="{Binding SaveProjectAsCommand}" InputGesture="Ctrl+Shift+S"/>
</MenuItem>
```

### 순환 참조
없음. Track→Clip 참조 없이 TrackIndex로 매핑.

### 테스트
```bash
dotnet test VortexCut.Tests --filter "FullyQualifiedName~Serialization"
```

---

## Phase 3B: Undo/Redo 시스템

### 목표
모든 편집 작업을 되돌리기(Ctrl+Z)/다시 실행(Ctrl+Shift+Z) 할 수 있어야 한다.

### 아키텍처: Command 패턴

```
MainViewModel
  └── TimelineViewModel
        └── UndoRedoManager
              ├── _undoStack: Stack<ITimelineCommand>
              └── _redoStack: Stack<ITimelineCommand>
```

### 구현 파일 목록

#### 1. 새 파일: `VortexCut.UI/Commands/ITimelineCommand.cs`

```csharp
namespace VortexCut.UI.Commands;

/// <summary>
/// Undo/Redo 가능한 편집 작업 인터페이스
/// </summary>
public interface ITimelineCommand
{
    /// <summary>작업 실행</summary>
    void Execute();

    /// <summary>작업 되돌리기</summary>
    void Undo();

    /// <summary>메뉴에 표시할 설명 (예: "Move Clip")</summary>
    string Description { get; }
}

/// <summary>
/// 여러 Command를 하나로 묶는 복합 Command (리플 편집 등)
/// </summary>
public class CompositeCommand : ITimelineCommand
{
    private readonly List<ITimelineCommand> _commands;
    public string Description { get; }

    public CompositeCommand(string description, IEnumerable<ITimelineCommand> commands)
    {
        Description = description;
        _commands = commands.ToList();
    }

    public void Execute()
    {
        foreach (var cmd in _commands) cmd.Execute();
    }

    public void Undo()
    {
        // 역순으로 되돌리기
        for (int i = _commands.Count - 1; i >= 0; i--)
            _commands[i].Undo();
    }
}
```

#### 2. 새 파일: `VortexCut.UI/Commands/UndoRedoManager.cs`

```csharp
namespace VortexCut.UI.Commands;

public class UndoRedoManager
{
    private readonly Stack<ITimelineCommand> _undoStack = new();
    private readonly Stack<ITimelineCommand> _redoStack = new();
    private const int MaxUndoCount = 100;

    public event Action? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string UndoDescription => CanUndo ? _undoStack.Peek().Description : "";
    public string RedoDescription => CanRedo ? _redoStack.Peek().Description : "";

    /// <summary>
    /// Command 실행 + Undo 스택에 추가
    /// </summary>
    public void Execute(ITimelineCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear(); // 새 작업 시 Redo 스택 초기화

        // 스택 크기 제한
        if (_undoStack.Count > MaxUndoCount)
            TrimStack(_undoStack, MaxUndoCount);

        StateChanged?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        StateChanged?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        StateChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    private static void TrimStack(Stack<ITimelineCommand> stack, int maxCount)
    {
        var items = stack.ToArray();
        stack.Clear();
        for (int i = Math.Min(items.Length - 1, maxCount - 1); i >= 0; i--)
            stack.Push(items[i]);
    }
}
```

#### 3. 새 파일: `VortexCut.UI/Commands/ClipCommands.cs`

```csharp
namespace VortexCut.UI.Commands;

/// <summary>클립 추가</summary>
public class AddClipCommand : ITimelineCommand
{
    private readonly TimelineViewModel _timeline;
    private readonly ClipModel _clip;
    public string Description => "Add Clip";

    public AddClipCommand(TimelineViewModel timeline, ClipModel clip)
    {
        _timeline = timeline;
        _clip = clip;
    }

    public void Execute() => _timeline.Clips.Add(_clip);
    public void Undo() => _timeline.Clips.Remove(_clip);
}

/// <summary>클립 삭제</summary>
public class RemoveClipCommand : ITimelineCommand
{
    private readonly TimelineViewModel _timeline;
    private readonly ClipModel _clip;
    public string Description => "Delete Clip";

    public RemoveClipCommand(TimelineViewModel timeline, ClipModel clip)
    {
        _timeline = timeline;
        _clip = clip;
    }

    public void Execute() => _timeline.Clips.Remove(_clip);
    public void Undo() => _timeline.Clips.Add(_clip);
}

/// <summary>클립 이동 (StartTimeMs 변경)</summary>
public class MoveClipCommand : ITimelineCommand
{
    private readonly ClipModel _clip;
    private readonly long _oldStartTime;
    private readonly long _newStartTime;
    private readonly int _oldTrackIndex;
    private readonly int _newTrackIndex;
    public string Description => "Move Clip";

    public MoveClipCommand(ClipModel clip, long newStartTime, int newTrackIndex)
    {
        _clip = clip;
        _oldStartTime = clip.StartTimeMs;
        _oldTrackIndex = clip.TrackIndex;
        _newStartTime = newStartTime;
        _newTrackIndex = newTrackIndex;
    }

    public void Execute()
    {
        _clip.StartTimeMs = _newStartTime;
        _clip.TrackIndex = _newTrackIndex;
    }

    public void Undo()
    {
        _clip.StartTimeMs = _oldStartTime;
        _clip.TrackIndex = _oldTrackIndex;
    }
}

/// <summary>클립 트림 (DurationMs 변경)</summary>
public class TrimClipCommand : ITimelineCommand
{
    private readonly ClipModel _clip;
    private readonly long _oldStartTime, _newStartTime;
    private readonly long _oldDuration, _newDuration;
    public string Description => "Trim Clip";

    public TrimClipCommand(ClipModel clip, long newStartTime, long newDuration)
    {
        _clip = clip;
        _oldStartTime = clip.StartTimeMs;
        _oldDuration = clip.DurationMs;
        _newStartTime = newStartTime;
        _newDuration = newDuration;
    }

    public void Execute()
    {
        _clip.StartTimeMs = _newStartTime;
        _clip.DurationMs = _newDuration;
    }

    public void Undo()
    {
        _clip.StartTimeMs = _oldStartTime;
        _clip.DurationMs = _oldDuration;
    }
}

/// <summary>클립 자르기 (Razor: 1개 → 2개)</summary>
public class CutClipCommand : ITimelineCommand
{
    private readonly TimelineViewModel _timeline;
    private readonly ClipModel _original;
    private readonly ClipModel _newClip;
    private readonly long _originalDuration;
    private readonly long _cutTime;
    public string Description => "Cut Clip";

    public CutClipCommand(TimelineViewModel timeline, ClipModel original, long cutTimeMs)
    {
        _timeline = timeline;
        _original = original;
        _cutTime = cutTimeMs;
        _originalDuration = original.DurationMs;

        // 새 클립 생성 (커트 포인트 이후)
        long rightDuration = original.StartTimeMs + original.DurationMs - cutTimeMs;
        _newClip = new ClipModel
        {
            // 원본 속성 복사 + 시간 조정
            FilePath = original.FilePath,
            StartTimeMs = cutTimeMs,
            DurationMs = rightDuration,
            TrackIndex = original.TrackIndex
        };
    }

    public void Execute()
    {
        _original.DurationMs = _cutTime - _original.StartTimeMs;
        _timeline.Clips.Add(_newClip);
    }

    public void Undo()
    {
        _timeline.Clips.Remove(_newClip);
        _original.DurationMs = _originalDuration;
    }
}
```

#### 4. 새 파일: `VortexCut.UI/Commands/MarkerCommands.cs`

```csharp
namespace VortexCut.UI.Commands;

public class AddMarkerCommand : ITimelineCommand
{
    private readonly TimelineViewModel _timeline;
    private readonly MarkerModel _marker;
    public string Description => "Add Marker";

    public AddMarkerCommand(TimelineViewModel timeline, MarkerModel marker)
    {
        _timeline = timeline;
        _marker = marker;
    }

    public void Execute() => _timeline.Markers.Add(_marker);
    public void Undo() => _timeline.Markers.Remove(_marker);
}

public class RemoveMarkerCommand : ITimelineCommand
{
    private readonly TimelineViewModel _timeline;
    private readonly MarkerModel _marker;
    public string Description => "Delete Marker";

    public RemoveMarkerCommand(TimelineViewModel timeline, MarkerModel marker)
    {
        _timeline = timeline;
        _marker = marker;
    }

    public void Execute() => _timeline.Markers.Remove(_marker);
    public void Undo() => _timeline.Markers.Add(_marker);
}
```

#### 5. 새 파일: `VortexCut.UI/Commands/KeyframeCommands.cs`

```csharp
namespace VortexCut.UI.Commands;

public class AddKeyframeCommand : ITimelineCommand
{
    private readonly KeyframeSystem _system;
    private readonly Keyframe _keyframe;
    public string Description => "Add Keyframe";

    public AddKeyframeCommand(KeyframeSystem system, double time, double value,
                              InterpolationType interp = InterpolationType.Linear)
    {
        _system = system;
        _keyframe = new Keyframe { Time = time, Value = value, Interpolation = interp };
    }

    public void Execute() => _system.AddKeyframe(_keyframe.Time, _keyframe.Value, _keyframe.Interpolation);
    public void Undo() => _system.RemoveKeyframe(_keyframe);
}

public class RemoveKeyframeCommand : ITimelineCommand
{
    private readonly KeyframeSystem _system;
    private readonly Keyframe _keyframe;
    public string Description => "Delete Keyframe";

    public RemoveKeyframeCommand(KeyframeSystem system, Keyframe keyframe)
    {
        _system = system;
        _keyframe = keyframe;
    }

    public void Execute() => _system.RemoveKeyframe(_keyframe);
    public void Undo() => _system.AddKeyframe(_keyframe.Time, _keyframe.Value, _keyframe.Interpolation);
}
```

#### 6. 수정 파일: `VortexCut.UI/ViewModels/TimelineViewModel.cs`

통합 방법:
```csharp
public partial class TimelineViewModel : ViewModelBase
{
    // Undo/Redo 매니저 추가
    public UndoRedoManager UndoRedo { get; } = new();

    // 기존 메서드를 Command 패턴으로 래핑
    // 변경 전:
    //   public void DeleteSelectedClip() { Clips.Remove(SelectedClip); }
    // 변경 후:
    public void DeleteSelectedClip()
    {
        if (SelectedClip == null) return;

        if (RippleModeEnabled)
        {
            // CompositeCommand로 묶기
            var commands = new List<ITimelineCommand>();
            // ... ripple 관련 커맨드들 추가
            UndoRedo.Execute(new CompositeCommand("Ripple Delete", commands));
        }
        else
        {
            UndoRedo.Execute(new RemoveClipCommand(this, SelectedClip));
        }
    }

    [RelayCommand]
    private void Undo() => UndoRedo.Undo();

    [RelayCommand]
    private void Redo() => UndoRedo.Redo();
}
```

### 필요한 Command 전체 목록 (15종)

| # | Command | 설명 | 변경 대상 |
|---|---------|------|-----------|
| 1 | AddClipCommand | 클립 추가 | Clips 컬렉션 |
| 2 | RemoveClipCommand | 클립 삭제 | Clips 컬렉션 |
| 3 | MoveClipCommand | 클립 이동 | StartTimeMs, TrackIndex |
| 4 | TrimClipCommand | 클립 트림 | StartTimeMs, DurationMs |
| 5 | CutClipCommand | 클립 자르기 | DurationMs + 새 클립 |
| 6 | AddTrackCommand | 트랙 추가 | VideoTracks/AudioTracks |
| 7 | RemoveTrackCommand | 트랙 삭제 | VideoTracks/AudioTracks |
| 8 | AddMarkerCommand | 마커 추가 | Markers 컬렉션 |
| 9 | RemoveMarkerCommand | 마커 삭제 | Markers 컬렉션 |
| 10 | AddKeyframeCommand | 키프레임 추가 | KeyframeSystem |
| 11 | RemoveKeyframeCommand | 키프레임 삭제 | KeyframeSystem |
| 12 | MoveKeyframeCommand | 키프레임 이동 | Keyframe.Time, Value |
| 13 | ChangeClipColorCommand | 클립 색상 변경 | ColorLabelArgb |
| 14 | ChangeTrackPropertyCommand | 트랙 속성 변경 | Mute/Solo/Lock/Height |
| 15 | LinkClipsCommand | 클립 링크/언링크 | LinkedAudioClipId |

### 단축키
- `Ctrl+Z`: Undo
- `Ctrl+Shift+Z` 또는 `Ctrl+Y`: Redo

---

## Phase 3C: Export Pipeline (내보내기)

### 목표
타임라인을 비디오 파일(MP4/MKV/WebM)로 내보낼 수 있어야 한다.

### 현재 상태
- ✅ `renderer_render_frame()` - 단일 프레임 렌더링 가능
- ✅ FFmpeg 디코더 구현 (ffmpeg-next 8.0)
- ❌ FFmpeg 인코더 없음
- ❌ 내보내기 설정 구조체 없음
- ❌ 진행률 보고 메커니즘 없음

### 구현 파일 목록

#### Rust 측

##### 1. 새 파일: `rust-engine/src/ffmpeg/encoder.rs`

```rust
use ffmpeg_next as ffmpeg;
use std::path::Path;

/// 내보내기 설정
pub struct EncoderConfig {
    pub output_path: String,
    pub width: u32,
    pub height: u32,
    pub fps: f64,
    pub codec: VideoCodec,
    pub bitrate_kbps: u32,
    pub quality: u8,         // CRF: 0(최상) ~ 51(최하), 권장 18~23
}

pub enum VideoCodec {
    H264,
    H265,
    VP9,
}

pub struct VideoEncoder {
    output_ctx: ffmpeg::format::context::Output,
    video_stream_idx: usize,
    encoder: ffmpeg::codec::encoder::Video,
    scaler: ffmpeg::software::scaling::Context,  // RGBA → YUV420P
    frame_count: u64,
    pts_step: i64,  // = time_base / fps
}

impl VideoEncoder {
    /// 인코더 생성
    pub fn new(config: &EncoderConfig) -> Result<Self, String> {
        // 1. Output context 생성 (확장자로 컨테이너 자동 결정)
        // 2. Video stream 추가
        // 3. Codec 설정 (코덱, 비트레이트, CRF, 프레임레이트)
        // 4. Scaler 생성 (RGBA → YUV420P)
        // 5. Header 작성
        todo!()
    }

    /// RGBA 프레임 인코딩
    pub fn encode_frame(&mut self, rgba_data: &[u8], timestamp_ms: i64) -> Result<(), String> {
        // 1. RGBA → YUV420P 변환 (scaler)
        // 2. PTS 설정
        // 3. encoder.send_frame()
        // 4. 패킷 수신 → output_ctx.write_packet()
        todo!()
    }

    /// 인코딩 완료 (flush + trailer)
    pub fn finalize(&mut self) -> Result<(), String> {
        // 1. encoder.send_eof()
        // 2. 남은 패킷 flush
        // 3. output_ctx.write_trailer()
        todo!()
    }
}

impl Drop for VideoEncoder {
    fn drop(&mut self) {
        let _ = self.finalize();
    }
}
```

##### 2. 새 파일: `rust-engine/src/ffi/encoder.rs`

```rust
use std::ffi::{c_char, c_void, CStr};
use crate::ffmpeg::encoder::{EncoderConfig, VideoCodec, VideoEncoder};

/// 인코더 생성
#[no_mangle]
pub extern "C" fn encoder_create(
    output_path: *const c_char,
    width: u32,
    height: u32,
    fps: f64,
    bitrate_kbps: u32,
    quality: u8,
    codec_id: i32,  // 0=H264, 1=H265, 2=VP9
    out_encoder: *mut *mut c_void,
) -> i32 {
    // CStr → String 변환, EncoderConfig 생성, VideoEncoder::new()
    todo!()
}

/// 프레임 인코딩
#[no_mangle]
pub extern "C" fn encoder_encode_frame(
    encoder: *mut c_void,
    frame_data: *const u8,
    frame_size: usize,
    timestamp_ms: i64,
) -> i32 {
    todo!()
}

/// 인코딩 완료 (flush)
#[no_mangle]
pub extern "C" fn encoder_finalize(encoder: *mut c_void) -> i32 {
    todo!()
}

/// 인코더 해제
#[no_mangle]
pub extern "C" fn encoder_destroy(encoder: *mut c_void) -> i32 {
    todo!()
}
```

##### 3. 수정 파일: `rust-engine/src/ffi/mod.rs`
```rust
pub mod encoder;  // 추가
```

##### 4. 수정 파일: `rust-engine/src/ffmpeg/mod.rs`
```rust
pub mod encoder;  // 추가
```

#### C# 측

##### 5. 수정 파일: `VortexCut.Interop/NativeMethods.cs`

추가할 P/Invoke:
```csharp
// Encoder FFI
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int encoder_create(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath,
    uint width, uint height, double fps,
    uint bitrateKbps, byte quality, int codecId,
    out IntPtr outEncoder);

[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int encoder_encode_frame(
    IntPtr encoder, IntPtr frameData, nuint frameSize, long timestampMs);

[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int encoder_finalize(IntPtr encoder);

[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int encoder_destroy(IntPtr encoder);
```

##### 6. 새 파일: `VortexCut.Interop/Services/ExportService.cs`

```csharp
namespace VortexCut.Interop.Services;

public class EncoderHandle : SafeHandle
{
    public EncoderHandle(IntPtr handle) : base(IntPtr.Zero, true) => SetHandle(handle);
    public override bool IsInvalid => handle == IntPtr.Zero;
    protected override bool ReleaseHandle()
        => NativeMethods.encoder_destroy(handle) == ErrorCodes.SUCCESS;
}

public enum VideoCodec { H264 = 0, H265 = 1, VP9 = 2 }

public class ExportSettings
{
    public string OutputPath { get; set; } = "";
    public uint Width { get; set; } = 1920;
    public uint Height { get; set; } = 1080;
    public double Fps { get; set; } = 30.0;
    public VideoCodec Codec { get; set; } = VideoCodec.H264;
    public uint BitrateKbps { get; set; } = 8000;
    public byte Quality { get; set; } = 18;  // CRF (0~51)
}

public class ExportService : IDisposable
{
    private EncoderHandle? _encoder;
    private readonly RenderService _renderService;

    public ExportService(RenderService renderService) => _renderService = renderService;

    public void CreateEncoder(ExportSettings settings)
    {
        int result = NativeMethods.encoder_create(
            settings.OutputPath, settings.Width, settings.Height, settings.Fps,
            settings.BitrateKbps, settings.Quality, (int)settings.Codec,
            out IntPtr encoderPtr);
        CheckError(result);
        _encoder = new EncoderHandle(encoderPtr);
    }

    /// <summary>
    /// 전체 타임라인 내보내기 (순차 프레임 렌더링 → 인코딩)
    /// </summary>
    public async Task ExportAsync(
        long durationMs, double fps,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        long frameIntervalMs = (long)(1000.0 / fps);
        long totalFrames = durationMs / frameIntervalMs;

        for (long frame = 0; frame < totalFrames; frame++)
        {
            ct.ThrowIfCancellationRequested();

            long timestampMs = frame * frameIntervalMs;

            // 1. 프레임 렌더링 (Rust)
            using var renderedFrame = _renderService.RenderFrame(timestampMs);

            // 2. 프레임 인코딩 (Rust FFmpeg)
            unsafe
            {
                fixed (byte* ptr = renderedFrame.Data)
                {
                    int result = NativeMethods.encoder_encode_frame(
                        _encoder!.DangerousGetHandle(),
                        (IntPtr)ptr,
                        (nuint)renderedFrame.Data.Length,
                        timestampMs);
                    CheckError(result);
                }
            }

            // 3. 진행률 보고
            int pct = (int)(frame * 100 / totalFrames);
            progress?.Report(pct);
        }

        // 4. 완료 (flush)
        int finalResult = NativeMethods.encoder_finalize(_encoder!.DangerousGetHandle());
        CheckError(finalResult);
        progress?.Report(100);
    }

    public void Dispose() => _encoder?.Dispose();

    private static void CheckError(int code)
    {
        if (code != ErrorCodes.SUCCESS)
            throw new RustException($"Export error code: {code}");
    }
}
```

### 내보내기 루프 도식

```
for frame in 0..totalFrames:
    ┌──────────────────┐     ┌──────────────────┐     ┌─────────────┐
    │ RenderFrame(ts)  │ ──> │ RGBA byte[] data  │ ──> │ EncodeFrame │
    │ (Rust Decoder)   │     │ (C# managed)      │     │ (Rust FFmpeg│
    └──────────────────┘     └──────────────────┘     │  Encoder)   │
                                                       └──────┬──────┘
                                                              │
                                                     ┌────────▼────────┐
                                                     │ output.mp4 파일  │
                                                     └─────────────────┘
```

---

## Phase 3D: 타임라인 렌더링 최적화

### 목표
프리뷰 재생 시 60fps를 달성하고, 불필요한 디코딩을 최소화한다.

### 최적화 전략 3가지

#### 1. FrameCache (LRU 캐시)

이미 디코딩된 프레임을 메모리에 캐싱하여 재디코딩을 방지한다.

```csharp
// VortexCut.UI/Services/FrameCacheService.cs

/// <summary>
/// LRU 프레임 캐시 (최대 300프레임, ~600MB @ 1080p)
/// </summary>
public class FrameCacheService
{
    private readonly int _maxFrames;
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly Dictionary<long, LinkedListNode<CacheEntry>> _cache = new();
    private long _totalBytes;
    private const long MaxCacheBytes = 600 * 1024 * 1024; // 600MB

    public FrameCacheService(int maxFrames = 300) => _maxFrames = maxFrames;

    /// <summary>캐시에서 프레임 조회 (히트 시 LRU 갱신)</summary>
    public byte[]? TryGet(long timestampMs)
    {
        if (_cache.TryGetValue(timestampMs, out var node))
        {
            // LRU 갱신: 맨 앞으로 이동
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            return node.Value.Data;
        }
        return null;
    }

    /// <summary>프레임 캐싱</summary>
    public void Put(long timestampMs, byte[] data, uint width, uint height)
    {
        // 이미 존재하면 갱신
        if (_cache.ContainsKey(timestampMs))
            return;

        // 용량 초과 시 LRU 제거
        while (_cache.Count >= _maxFrames || _totalBytes + data.Length > MaxCacheBytes)
        {
            if (_lruList.Last == null) break;
            Evict();
        }

        var entry = new CacheEntry(timestampMs, data, width, height);
        var node = _lruList.AddFirst(entry);
        _cache[timestampMs] = node;
        _totalBytes += data.Length;
    }

    /// <summary>타임라인 변경 시 영향받는 구간만 무효화</summary>
    public void InvalidateRange(long startMs, long endMs)
    {
        var keysToRemove = _cache.Keys.Where(k => k >= startMs && k <= endMs).ToList();
        foreach (var key in keysToRemove)
            Remove(key);
    }

    /// <summary>전체 캐시 초기화</summary>
    public void Clear()
    {
        _cache.Clear();
        _lruList.Clear();
        _totalBytes = 0;
    }

    private void Evict()
    {
        var last = _lruList.Last!;
        _cache.Remove(last.Value.TimestampMs);
        _totalBytes -= last.Value.Data.Length;
        _lruList.RemoveLast();
    }

    private void Remove(long key)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            _totalBytes -= node.Value.Data.Length;
            _lruList.Remove(node);
            _cache.Remove(key);
        }
    }

    private record CacheEntry(long TimestampMs, byte[] Data, uint Width, uint Height);
}
```

#### 2. Background Pre-rendering (선행 렌더링)

Playhead 앞쪽 프레임을 백그라운드 스레드에서 미리 렌더링한다.

```csharp
// VortexCut.UI/Services/PreRenderService.cs

/// <summary>
/// Playhead 앞쪽 N프레임을 백그라운드에서 미리 렌더링
/// </summary>
public class PreRenderService : IDisposable
{
    private readonly FrameCacheService _cache;
    private readonly RenderService _renderer;
    private CancellationTokenSource? _cts;
    private Task? _preRenderTask;

    private const int LookAheadFrames = 30;  // 1초 분량 (30fps)

    public PreRenderService(FrameCacheService cache, RenderService renderer)
    {
        _cache = cache;
        _renderer = renderer;
    }

    /// <summary>
    /// 현재 시간부터 LookAhead만큼 미리 렌더링 시작
    /// </summary>
    public void StartPreRendering(long currentTimeMs, double fps)
    {
        Stop(); // 이전 작업 취소

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        long intervalMs = (long)(1000.0 / fps);

        _preRenderTask = Task.Run(() =>
        {
            for (int i = 1; i <= LookAheadFrames; i++)
            {
                if (ct.IsCancellationRequested) break;

                long ts = currentTimeMs + (i * intervalMs);

                // 캐시에 이미 있으면 스킵
                if (_cache.TryGet(ts) != null) continue;

                try
                {
                    using var frame = _renderer.RenderFrame(ts);
                    _cache.Put(ts, frame.Data, frame.Width, frame.Height);
                }
                catch
                {
                    break; // 타임라인 끝 등
                }
            }
        }, ct);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();
}
```

#### 3. PreviewViewModel 통합

```csharp
// VortexCut.UI/ViewModels/PreviewViewModel.cs 수정

public partial class PreviewViewModel : ViewModelBase
{
    private readonly FrameCacheService _frameCache = new();
    private readonly PreRenderService _preRenderer;

    /// <summary>프레임 표시 (캐시 우선)</summary>
    public void DisplayFrame(long timestampMs)
    {
        // 1. 캐시 히트 확인
        var cached = _frameCache.TryGet(timestampMs);
        if (cached != null)
        {
            UpdatePreviewImage(cached);
            return;
        }

        // 2. 캐시 미스 → 렌더링
        using var frame = _renderService.RenderFrame(timestampMs);
        _frameCache.Put(timestampMs, frame.Data, frame.Width, frame.Height);
        UpdatePreviewImage(frame.Data);

        // 3. 선행 렌더링 시작
        _preRenderer.StartPreRendering(timestampMs, _fps);
    }
}
```

### 성능 목표

| 항목 | 현재 | 최적화 후 |
|------|------|-----------|
| 캐시 히트 시 | N/A | < 1ms |
| 캐시 미스 시 | ~30ms | ~30ms (변동 없음) |
| 연속 재생 | 매 프레임 디코딩 | 선행 렌더링으로 캐시 히트 |
| 메모리 사용 | 프레임당 할당/해제 | LRU 600MB 풀 |
| Seek 시 | 항상 디코딩 | 최근 방문 구간 캐시 히트 |

---

## 구현 순서 (권장)

```
Phase 3A: 프로젝트 저장/불러오기 ──── 선행 조건 없음
Phase 3B: Undo/Redo ───────────── Phase 3A 이후 (저장 시 Undo 스택 초기화)
Phase 3C: Export Pipeline ────────── Phase 3A 이후 (프로젝트 설정 필요)
Phase 3D: 렌더링 최적화 ──────────── 독립적 (언제든 가능)
```

---

## 기존 파일 참조

| 파일 | 역할 | Phase |
|------|------|-------|
| `VortexCut.UI/Services/ProjectService.cs` | 프로젝트 관리 | 3A |
| `VortexCut.UI/ViewModels/MainViewModel.cs` | 메인 ViewModel | 3A, 3B |
| `VortexCut.UI/ViewModels/TimelineViewModel.cs` | 타임라인 상태 | 3A, 3B |
| `VortexCut.UI/ViewModels/PreviewViewModel.cs` | 프리뷰 렌더링 | 3D |
| `VortexCut.Core/Models/ClipModel.cs` | 클립 모델 | 3A |
| `VortexCut.Core/Models/TrackModel.cs` | 트랙 모델 | 3A |
| `VortexCut.Core/Models/MarkerModel.cs` | 마커 모델 | 3A |
| `VortexCut.Core/Models/KeyframeModel.cs` | 키프레임 모델 | 3A |
| `VortexCut.Interop/NativeMethods.cs` | P/Invoke 선언 | 3C |
| `VortexCut.Interop/Services/RenderService.cs` | 렌더링 서비스 | 3C, 3D |
| `VortexCut.Interop/Services/TimelineService.cs` | 타임라인 FFI | 3A |
| `rust-engine/src/ffmpeg/decoder.rs` | FFmpeg 디코더 | 3C 참고 |
| `rust-engine/src/rendering/renderer.rs` | 프레임 렌더러 | 3C, 3D |
| `rust-engine/src/ffi/renderer.rs` | Renderer FFI | 3C |

---

**작성자**: Claude Opus 4.6
**작성일**: 2026-02-11
