namespace VortexCut.Core.Models;

/// <summary>
/// 자막 위치
/// </summary>
public enum SubtitlePosition
{
    /// <summary>상단</summary>
    Top,
    /// <summary>중앙</summary>
    Center,
    /// <summary>하단 (기본)</summary>
    Bottom
}

/// <summary>
/// 자막 스타일 설정
/// </summary>
public class SubtitleStyle
{
    /// <summary>폰트 (기본: Arial)</summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>폰트 크기 (기본: 48)</summary>
    public double FontSize { get; set; } = 48;

    /// <summary>폰트 색상 ARGB (기본: 흰색)</summary>
    public uint FontColorArgb { get; set; } = 0xFFFFFFFF;

    /// <summary>외곽선 색상 ARGB (기본: 검정)</summary>
    public uint OutlineColorArgb { get; set; } = 0xFF000000;

    /// <summary>외곽선 두께 (기본: 2.0)</summary>
    public double OutlineThickness { get; set; } = 2.0;

    /// <summary>배경 색상 ARGB (기본: 반투명 검정)</summary>
    public uint BackgroundColorArgb { get; set; } = 0x80000000;

    /// <summary>자막 위치 (기본: 하단)</summary>
    public SubtitlePosition Position { get; set; } = SubtitlePosition.Bottom;

    /// <summary>굵게</summary>
    public bool IsBold { get; set; } = false;

    /// <summary>기울임</summary>
    public bool IsItalic { get; set; } = false;

    /// <summary>깊은 복사</summary>
    public SubtitleStyle Clone() => new()
    {
        FontFamily = FontFamily,
        FontSize = FontSize,
        FontColorArgb = FontColorArgb,
        OutlineColorArgb = OutlineColorArgb,
        OutlineThickness = OutlineThickness,
        BackgroundColorArgb = BackgroundColorArgb,
        Position = Position,
        IsBold = IsBold,
        IsItalic = IsItalic,
    };
}
