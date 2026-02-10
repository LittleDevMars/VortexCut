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
