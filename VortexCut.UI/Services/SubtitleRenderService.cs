using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using VortexCut.Core.Models;

namespace VortexCut.UI.Services;

/// <summary>
/// 자막 텍스트 → RGBA 비트맵 렌더링 (Export 자막 번인용)
/// Avalonia의 FormattedText를 사용하여 텍스트를 RGBA 비트맵으로 변환
/// </summary>
public static class SubtitleRenderService
{
    /// <summary>
    /// 자막 오버레이 RGBA 비트맵 데이터
    /// </summary>
    public record SubtitleBitmap(
        int X, int Y,
        int Width, int Height,
        byte[] RgbaData);

    /// <summary>
    /// 자막 텍스트를 RGBA 비트맵으로 렌더링
    /// </summary>
    /// <param name="text">자막 텍스트</param>
    /// <param name="style">자막 스타일</param>
    /// <param name="videoWidth">비디오 해상도 너비</param>
    /// <param name="videoHeight">비디오 해상도 높이</param>
    public static SubtitleBitmap RenderSubtitle(
        string text,
        SubtitleStyle style,
        int videoWidth,
        int videoHeight)
    {
        // 텍스트 측정 (FormattedText 사용)
        var typeface = new Typeface(
            style.FontFamily,
            style.IsItalic ? FontStyle.Italic : FontStyle.Normal,
            style.IsBold ? FontWeight.Bold : FontWeight.Normal);

        var maxWidth = videoWidth * 0.85; // 최대 너비 85%

        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            style.FontSize,
            Brushes.White);
        formattedText.MaxTextWidth = maxWidth;

        // 텍스트 크기 계산
        int textWidth = (int)Math.Ceiling(formattedText.Width);
        int textHeight = (int)Math.Ceiling(formattedText.Height);

        // 패딩
        int padX = 20;
        int padY = 10;
        int bmpWidth = textWidth + padX * 2;
        int bmpHeight = textHeight + padY * 2;

        // 짝수 크기 보장 (YUV420P 호환)
        if (bmpWidth % 2 != 0) bmpWidth++;
        if (bmpHeight % 2 != 0) bmpHeight++;

        // RGBA 비트맵 생성 (수동 렌더링)
        byte[] rgba = new byte[bmpWidth * bmpHeight * 4];

        // 배경 색상
        byte bgR = (byte)((style.BackgroundColorArgb >> 16) & 0xFF);
        byte bgG = (byte)((style.BackgroundColorArgb >> 8) & 0xFF);
        byte bgB = (byte)(style.BackgroundColorArgb & 0xFF);
        byte bgA = (byte)((style.BackgroundColorArgb >> 24) & 0xFF);

        // 배경 채우기
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = bgR;
            rgba[i + 1] = bgG;
            rgba[i + 2] = bgB;
            rgba[i + 3] = bgA;
        }

        // 텍스트 렌더링 (RenderTargetBitmap 사용)
        RenderTextToRgba(rgba, bmpWidth, bmpHeight, padX, padY,
            text, typeface, style.FontSize,
            style.FontColorArgb, style.OutlineColorArgb, style.OutlineThickness);

        // 위치 계산
        int x = (videoWidth - bmpWidth) / 2; // 수평 중앙
        int y = style.Position switch
        {
            SubtitlePosition.Top => (int)(videoHeight * 0.05),
            SubtitlePosition.Center => (videoHeight - bmpHeight) / 2,
            _ => (int)(videoHeight * 0.85) - bmpHeight, // Bottom
        };

        return new SubtitleBitmap(x, y, bmpWidth, bmpHeight, rgba);
    }

    /// <summary>
    /// 간단한 텍스트 렌더링 (비트맵 픽셀 기반)
    /// Avalonia RenderTargetBitmap을 사용하여 정확한 텍스트 렌더링
    /// </summary>
    private static void RenderTextToRgba(
        byte[] rgba, int bmpWidth, int bmpHeight,
        int offsetX, int offsetY,
        string text, Typeface typeface, double fontSize,
        uint fontColorArgb, uint outlineColorArgb, double outlineThickness)
    {
        try
        {
            // Avalonia RenderTargetBitmap으로 텍스트 렌더링
            var pixelSize = new PixelSize(bmpWidth, bmpHeight);
            using var renderTarget = new Avalonia.Media.Imaging.RenderTargetBitmap(pixelSize, new Vector(96, 96));

            using (var ctx = renderTarget.CreateDrawingContext())
            {
                var fontBrush = new SolidColorBrush(Color.FromUInt32(fontColorArgb));
                var maxWidth = bmpWidth - offsetX * 2;

                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    fontBrush);
                formattedText.MaxTextWidth = maxWidth;

                // 외곽선 (outline) — 4방향 오프셋으로 근사
                if (outlineThickness > 0)
                {
                    var outlineBrush = new SolidColorBrush(Color.FromUInt32(outlineColorArgb));
                    var outlineText = new FormattedText(
                        text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        outlineBrush);
                    outlineText.MaxTextWidth = maxWidth;

                    double t = outlineThickness;
                    var offsets = new[] {
                        (-t, 0), (t, 0), (0, -t), (0, t),
                        (-t, -t), (t, -t), (-t, t), (t, t)
                    };

                    foreach (var (dx, dy) in offsets)
                    {
                        ctx.DrawText(outlineText, new Point(offsetX + dx, offsetY + dy));
                    }
                }

                // 본문 텍스트
                ctx.DrawText(formattedText, new Point(offsetX, offsetY));
            }

            // RenderTargetBitmap → byte[] 복사 (CopyPixels → BGRA → RGBA 변환)
            int stride = bmpWidth * 4;
            int totalBytes = stride * bmpHeight;
            IntPtr buffer = Marshal.AllocHGlobal(totalBytes);
            try
            {
                renderTarget.CopyPixels(
                    new PixelRect(0, 0, bmpWidth, bmpHeight),
                    buffer, totalBytes, stride);

                unsafe
                {
                    byte* src = (byte*)buffer;
                    for (int row = 0; row < bmpHeight; row++)
                    {
                        for (int col = 0; col < bmpWidth; col++)
                        {
                            int srcIdx = row * stride + col * 4;
                            int dstIdx = (row * bmpWidth + col) * 4;

                            // BGRA → RGBA 변환
                            byte b = src[srcIdx];
                            byte g = src[srcIdx + 1];
                            byte r = src[srcIdx + 2];
                            byte a = src[srcIdx + 3];

                            if (a > 0)
                            {
                                rgba[dstIdx] = r;
                                rgba[dstIdx + 1] = g;
                                rgba[dstIdx + 2] = b;
                                rgba[dstIdx + 3] = a;
                            }
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            // 텍스트 렌더링 실패 — 배경만 유지
            System.Diagnostics.Debug.WriteLine($"SubtitleRenderService: 텍스트 렌더링 실패: {ex.Message}");
        }
    }
}
