namespace VortexCut.Core.Models;

/// <summary>
/// 마커 타입
/// </summary>
public enum MarkerType
{
    Comment,   // 일반 코멘트 마커
    Chapter,   // 챕터 마커 (챕터 구분)
    Region     // 영역 마커 (구간 표시)
}

/// <summary>
/// 타임라인 마커 모델
/// </summary>
public class MarkerModel
{
    public ulong Id { get; set; }
    public long TimeMs { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public uint ColorArgb { get; set; } = 0xFFFFFF00; // Yellow (ARGB)
    public MarkerType Type { get; set; } = MarkerType.Comment;

    // 영역 마커용 (Region)
    public long? DurationMs { get; set; }

    /// <summary>
    /// 영역 마커인지 여부
    /// </summary>
    public bool IsRegion => Type == MarkerType.Region && DurationMs.HasValue;

    /// <summary>
    /// 영역 마커의 끝 시간
    /// </summary>
    public long EndTimeMs => TimeMs + (DurationMs ?? 0);

    public MarkerModel() { }

    public MarkerModel(ulong id, long timeMs, string name, MarkerType type = MarkerType.Comment)
    {
        Id = id;
        TimeMs = timeMs;
        Name = name;
        Type = type;
    }
}
