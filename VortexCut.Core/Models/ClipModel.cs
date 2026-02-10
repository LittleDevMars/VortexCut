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
