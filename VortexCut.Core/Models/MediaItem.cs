namespace VortexCut.Core.Models;

/// <summary>
/// Project Bin에 표시될 미디어 아이템
/// </summary>
public class MediaItem
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaType Type { get; set; }
    public long DurationMs { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;

    // 비디오 메타데이터
    public uint Width { get; set; }
    public uint Height { get; set; }
    public double Fps { get; set; }

    // 오디오 메타데이터
    public int SampleRate { get; set; }
    public int Channels { get; set; }

    /// <summary>
    /// 프리뷰/Filmstrip용 저해상도 Proxy 파일 경로 (선택 사항)
    /// </summary>
    public string? ProxyFilePath { get; set; }
}

public enum MediaType
{
    Video,
    Audio,
    Image,
    Sequence
}
