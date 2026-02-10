using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Controls.Timeline;

/// <summary>
/// 타임라인 상단 헤더 (시간 눈금, 마커)
/// </summary>
public class TimelineHeader : Control
{
    private const double HeaderHeight = 80;
    private TimelineViewModel? _viewModel;
    private double _pixelsPerMs = 0.1;
    private double _scrollOffsetX = 0;

    public void SetViewModel(TimelineViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void SetZoom(double pixelsPerMs)
    {
        _pixelsPerMs = Math.Clamp(pixelsPerMs, 0.01, 1.0);
        InvalidateVisual();
    }

    public void SetScrollOffset(double offsetX)
    {
        _scrollOffsetX = offsetX;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 배경
        var headerBrush = new SolidColorBrush(Color.Parse("#2D2D30"));
        var headerRect = new Rect(0, 0, Bounds.Width, HeaderHeight);
        context.FillRectangle(headerBrush, headerRect);

        // 시간 눈금
        DrawTimeRuler(context);

        // 마커 (Phase 2D)
        if (_viewModel != null)
        {
            DrawMarkers(context);
        }
    }

    private void DrawTimeRuler(DrawingContext context)
    {
        var textBrush = new SolidColorBrush(Colors.White);
        var typeface = new Typeface("Segoe UI");
        var fontSize = 10;

        for (int i = 0; i < 100; i++)
        {
            long timeMs = i * 1000; // 1초 간격
            double x = TimeToX(timeMs);

            if (x < 0 || x > Bounds.Width)
                continue;

            // 큰 눈금
            context.DrawLine(new Pen(Brushes.Gray, 1),
                new Point(x, HeaderHeight - 10),
                new Point(x, HeaderHeight));

            // 시간 텍스트
            var text = new FormattedText(
                $"{i}s",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                textBrush);

            context.DrawText(text, new Point(x + 2, HeaderHeight - 25));
        }
    }

    private void DrawMarkers(DrawingContext context)
    {
        // Phase 2D: 마커 렌더링
        // foreach (var marker in _viewModel.Markers) { ... }
    }

    private double TimeToX(long timeMs)
    {
        return timeMs * _pixelsPerMs - _scrollOffsetX;
    }
}
