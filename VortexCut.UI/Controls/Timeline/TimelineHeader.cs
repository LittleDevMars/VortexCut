using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VortexCut.Core.Models;
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
    private MarkerModel? _hoveredMarker;

    public void SetViewModel(TimelineViewModel viewModel)
    {
        _viewModel = viewModel;

        // 마커 추가/삭제 시 자동 새로고침
        _viewModel.Markers.CollectionChanged += (s, e) => InvalidateVisual();
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
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var fontSize = 10;

        // 작은 눈금 (100ms 간격)
        var smallTickPen = new Pen(new SolidColorBrush(Color.Parse("#555555")), 1);
        for (int i = 0; i < 1000; i++)
        {
            long timeMs = i * 100; // 100ms 간격
            double x = TimeToX(timeMs);

            if (x < 0 || x > Bounds.Width)
                continue;

            // 작은 눈금 (5px)
            if (i % 10 != 0) // 1초 단위는 큰 눈금으로
            {
                context.DrawLine(smallTickPen,
                    new Point(x, HeaderHeight - 5),
                    new Point(x, HeaderHeight));
            }
        }

        // 큰 눈금 및 텍스트 (1초 간격)
        var bigTickPen = new Pen(new SolidColorBrush(Color.Parse("#AAAAAA")), 1.5);
        for (int i = 0; i < 100; i++)
        {
            long timeMs = i * 1000; // 1초 간격
            double x = TimeToX(timeMs);

            if (x < 0 || x > Bounds.Width)
                continue;

            // 큰 눈금 (12px)
            context.DrawLine(bigTickPen,
                new Point(x, HeaderHeight - 12),
                new Point(x, HeaderHeight));

            // 시간 텍스트 (SemiBold, 배경 추가)
            var text = new FormattedText(
                $"{i}s",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.White);

            // 텍스트 배경 (반투명)
            var textBgRect = new Rect(x + 1, HeaderHeight - 26, text.Width + 4, text.Height + 2);
            context.FillRectangle(
                new SolidColorBrush(Color.FromArgb(150, 45, 45, 48)),
                textBgRect);

            context.DrawText(text, new Point(x + 3, HeaderHeight - 25));
        }
    }

    private void DrawMarkers(DrawingContext context)
    {
        if (_viewModel == null) return;

        foreach (var marker in _viewModel.Markers)
        {
            double x = TimeToX(marker.TimeMs);

            // 화면 밖이면 스킵 (성능 최적화)
            if (x < -20 || x > Bounds.Width + 20)
                continue;

            var color = ArgbToColor(marker.ColorArgb);
            var brush = new SolidColorBrush(color);

            // 삼각형 그리기 (▼)
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var size = marker.Type == MarkerType.Chapter ? 12.0 : 8.0;
                var yTop = 20.0;

                ctx.BeginFigure(new Point(x, yTop), true);
                ctx.LineTo(new Point(x - size / 2, yTop + size));
                ctx.LineTo(new Point(x + size / 2, yTop + size));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(brush, null, geometry);

            // Region 마커: 수평선 추가
            if (marker.IsRegion)
            {
                double endX = TimeToX(marker.EndTimeMs);
                var pen = new Pen(brush, 2);
                context.DrawLine(pen, new Point(x, 30), new Point(endX, 30));

                // 끝 삼각형
                var endGeometry = new StreamGeometry();
                using (var ctx = endGeometry.Open())
                {
                    var size = marker.Type == MarkerType.Chapter ? 12.0 : 8.0;
                    var yTop = 20.0;

                    ctx.BeginFigure(new Point(endX, yTop), true);
                    ctx.LineTo(new Point(endX - size / 2, yTop + size));
                    ctx.LineTo(new Point(endX + size / 2, yTop + size));
                    ctx.EndFigure(true);
                }
                context.DrawGeometry(brush, null, endGeometry);
            }
        }

        // 호버된 마커 툴팁
        if (_hoveredMarker != null)
        {
            DrawMarkerTooltip(context, _hoveredMarker);
        }
    }

    private void DrawMarkerTooltip(DrawingContext context, MarkerModel marker)
    {
        double x = TimeToX(marker.TimeMs);
        double y = 45;

        var typeface = new Typeface("Segoe UI");
        var text = new FormattedText(
            marker.Name,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            Brushes.White);

        // 배경 박스
        var padding = 4;
        var bgRect = new Rect(
            x - padding,
            y - padding,
            text.Width + padding * 2,
            text.Height + padding * 2);

        context.FillRectangle(
            new SolidColorBrush(Color.FromArgb(220, 40, 40, 40)),
            bgRect);

        context.DrawText(text, new Point(x, y));
    }

    private Color ArgbToColor(uint argb)
    {
        byte a = (byte)((argb >> 24) & 0xFF);
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    private MarkerModel? GetMarkerAtPosition(Point point, double threshold = 20)
    {
        if (_viewModel == null) return null;

        foreach (var marker in _viewModel.Markers)
        {
            double x = TimeToX(marker.TimeMs);
            if (Math.Abs(point.X - x) < threshold && point.Y < 40)
                return marker;
        }

        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_viewModel == null) return;

        var point = e.GetPosition(this);
        var clickedMarker = GetMarkerAtPosition(point, threshold: 20);

        if (clickedMarker != null)
        {
            _viewModel.CurrentTimeMs = clickedMarker.TimeMs;
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetPosition(this);
        var newHoveredMarker = GetMarkerAtPosition(point, threshold: 20);

        if (newHoveredMarker != _hoveredMarker)
        {
            _hoveredMarker = newHoveredMarker;
            InvalidateVisual();
        }
    }

    private double TimeToX(long timeMs)
    {
        return timeMs * _pixelsPerMs - _scrollOffsetX;
    }
}
