using System.ComponentModel;

namespace VortexCut.Core.Models;

/// <summary>
/// 자막 클립 모델 — ClipModel 상속 + 텍스트/스타일 정보
/// </summary>
public class SubtitleClipModel : ClipModel
{
    /// <summary>자막 텍스트 (멀티라인 지원)</summary>
    [Category("자막")]
    [DisplayName("텍스트")]
    public string Text { get; set; } = string.Empty;

    /// <summary>자막 스타일</summary>
    [Browsable(false)]
    public SubtitleStyle Style { get; set; } = new();

    public SubtitleClipModel() { }

    public SubtitleClipModel(ulong id, long startTimeMs, long durationMs, string text, int trackIndex = 0)
        : base(id, string.Empty, startTimeMs, durationMs, trackIndex)
    {
        Text = text;
    }
}
