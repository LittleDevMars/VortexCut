namespace VortexCut.Core.Models;

/// <summary>
/// 타임라인 클립 모델
/// </summary>
public class ClipModel
{
    public ulong Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long StartTimeMs { get; set; }
    public long DurationMs { get; set; }
    public int TrackIndex { get; set; }

    // 링크된 클립 (비디오+오디오 연결)
    public ulong? LinkedAudioClipId { get; set; }
    public ulong? LinkedVideoClipId { get; set; }

    /// <summary>
    /// 다른 클립과 링크되어 있는지 여부
    /// </summary>
    public bool IsLinked => LinkedAudioClipId.HasValue || LinkedVideoClipId.HasValue;

    // 키프레임 시스템 (After Effects 스타일)
    public KeyframeSystem OpacityKeyframes { get; } = new KeyframeSystem();
    public KeyframeSystem VolumeKeyframes { get; } = new KeyframeSystem();
    public KeyframeSystem PositionXKeyframes { get; } = new KeyframeSystem();
    public KeyframeSystem PositionYKeyframes { get; } = new KeyframeSystem();
    public KeyframeSystem ScaleKeyframes { get; } = new KeyframeSystem();
    public KeyframeSystem RotationKeyframes { get; } = new KeyframeSystem();

    /// <summary>
    /// 애니메이션이 활성화되어 있는지 여부 (키프레임 존재 여부)
    /// </summary>
    public bool HasAnimation =>
        OpacityKeyframes.Keyframes.Count > 0 ||
        VolumeKeyframes.Keyframes.Count > 0 ||
        PositionXKeyframes.Keyframes.Count > 0 ||
        PositionYKeyframes.Keyframes.Count > 0 ||
        ScaleKeyframes.Keyframes.Count > 0 ||
        RotationKeyframes.Keyframes.Count > 0;

    public ClipModel() { }

    public ClipModel(ulong id, string filePath, long startTimeMs, long durationMs, int trackIndex = 0)
    {
        Id = id;
        FilePath = filePath;
        StartTimeMs = startTimeMs;
        DurationMs = durationMs;
        TrackIndex = trackIndex;
    }

    public long EndTimeMs => StartTimeMs + DurationMs;
}
