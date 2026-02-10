using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using VortexCut.Core.Models;

namespace VortexCut.UI.Controls;

/// <summary>
/// Kdenlive 스타일 타임라인 캔버스
/// </summary>
public class TimelineCanvas : Control
{
    private const double TrackHeight = 60;
    private const double TimelineHeight = 80;
    private const double MinClipWidth = 20;

    private List<ClipModel> _clips = new();
    private double _pixelsPerMs = 0.1; // Zoom level
    private double _scrollOffsetX = 0;
    private ClipModel? _selectedClip;
    private ClipModel? _draggingClip;
    private Point _dragStartPoint;
    private bool _isDragging;

    /// <summary>
    /// ViewModel 참조 (드롭 시 클립 추가용)
    /// </summary>
    public ViewModels.TimelineViewModel? ViewModel { get; set; }

    public TimelineCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        DragDrop.SetAllowDrop(this, true);

        // DragDrop 이벤트 등록
        AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    /// <summary>
    /// 클립 목록 설정
    /// </summary>
    public void SetClips(IEnumerable<ClipModel> clips)
    {
        _clips = new List<ClipModel>(clips);
        InvalidateVisual();
    }

    /// <summary>
    /// Zoom 레벨 설정
    /// </summary>
    public void SetZoom(double pixelsPerMs)
    {
        _pixelsPerMs = Math.Clamp(pixelsPerMs, 0.01, 1.0);
        InvalidateVisual();
    }

    /// <summary>
    /// 스크롤 오프셋 설정
    /// </summary>
    public void SetScrollOffset(double offsetX)
    {
        _scrollOffsetX = offsetX;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 배경
        context.FillRectangle(new SolidColorBrush(Color.Parse("#1E1E1E")), Bounds);

        // 타임라인 헤더
        DrawTimelineHeader(context);

        // 트랙들
        DrawTracks(context);

        // 클립들
        DrawClips(context);

        // Playhead (타임 인디케이터)
        DrawPlayhead(context);
    }

    private void DrawTimelineHeader(DrawingContext context)
    {
        var headerBrush = new SolidColorBrush(Color.Parse("#2D2D30"));
        var headerRect = new Rect(0, 0, Bounds.Width, TimelineHeight);
        context.FillRectangle(headerBrush, headerRect);

        // 시간 눈금
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
                new Point(x, TimelineHeight - 10),
                new Point(x, TimelineHeight));

            // 시간 텍스트
            var text = new FormattedText(
                $"{i}s",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                textBrush);

            context.DrawText(text, new Point(x + 2, TimelineHeight - 25));
        }
    }

    private void DrawTracks(DrawingContext context)
    {
        var trackBrush = new SolidColorBrush(Color.Parse("#2D2D30"));
        var borderPen = new Pen(new SolidColorBrush(Color.Parse("#444444")), 1);

        for (int i = 0; i < 5; i++) // 5개 트랙
        {
            double y = TimelineHeight + i * TrackHeight;
            var trackRect = new Rect(0, y, Bounds.Width, TrackHeight);

            context.FillRectangle(trackBrush, trackRect);
            context.DrawRectangle(borderPen, trackRect);

            // 트랙 레이블
            var labelText = new FormattedText(
                $"V{5 - i}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.White);

            context.DrawText(labelText, new Point(10, y + TrackHeight / 2 - 10));
        }
    }

    private void DrawClips(DrawingContext context)
    {
        foreach (var clip in _clips)
        {
            DrawClip(context, clip, clip == _selectedClip);
        }
    }

    private void DrawClip(DrawingContext context, ClipModel clip, bool isSelected)
    {
        double x = TimeToX(clip.StartTimeMs);
        double width = DurationToWidth(clip.DurationMs);
        double y = TimelineHeight + clip.TrackIndex * TrackHeight + 5;
        double height = TrackHeight - 10;

        var clipRect = new Rect(x, y, Math.Max(width, MinClipWidth), height);

        // 클립 배경
        var clipBrush = isSelected
            ? new SolidColorBrush(Color.Parse("#007ACC"))
            : new SolidColorBrush(Color.Parse("#3E3E42"));

        context.FillRectangle(clipBrush, clipRect);

        // 테두리
        var borderPen = isSelected
            ? new Pen(Brushes.White, 2)
            : new Pen(new SolidColorBrush(Color.Parse("#555555")), 1);

        context.DrawRectangle(borderPen, clipRect);

        // 클립 이름
        var fileName = System.IO.Path.GetFileName(clip.FilePath);
        var text = new FormattedText(
            fileName,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White);

        context.DrawText(text, new Point(x + 5, y + 5));
    }

    private void DrawPlayhead(DrawingContext context)
    {
        // TODO: 현재 재생 위치 표시
        double x = TimeToX(0); // 임시로 0ms
        var pen = new Pen(Brushes.Red, 2);
        context.DrawLine(pen,
            new Point(x, 0),
            new Point(x, Bounds.Height));
    }

    private double TimeToX(long timeMs)
    {
        return timeMs * _pixelsPerMs - _scrollOffsetX;
    }

    private double DurationToWidth(long durationMs)
    {
        return durationMs * _pixelsPerMs;
    }

    private long XToTime(double x)
    {
        return (long)((x + _scrollOffsetX) / _pixelsPerMs);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);

        // 클립 선택
        _selectedClip = GetClipAtPosition(point);

        if (_selectedClip != null)
        {
            _isDragging = true;
            _draggingClip = _selectedClip;
            _dragStartPoint = point;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isDragging && _draggingClip != null)
        {
            var point = e.GetPosition(this);
            var deltaX = point.X - _dragStartPoint.X;

            // 드래그로 클립 이동
            var deltaTimeMs = (long)(deltaX / _pixelsPerMs);
            _draggingClip.StartTimeMs = Math.Max(0, _draggingClip.StartTimeMs + deltaTimeMs);

            _dragStartPoint = point;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _isDragging = false;
        _draggingClip = null;
        e.Handled = true;
    }

    private ClipModel? GetClipAtPosition(Point point)
    {
        foreach (var clip in _clips)
        {
            double x = TimeToX(clip.StartTimeMs);
            double width = DurationToWidth(clip.DurationMs);
            double y = TimelineHeight + clip.TrackIndex * TrackHeight + 5;
            double height = TrackHeight - 10;

            var clipRect = new Rect(x, y, Math.Max(width, MinClipWidth), height);

            if (clipRect.Contains(point))
            {
                return clip;
            }
        }

        return null;
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("MediaItem"))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("MediaItem"))
        {
            var mediaItem = e.Data.Get("MediaItem") as MediaItem;
            if (mediaItem != null && ViewModel != null)
            {
                var dropPoint = e.GetPosition(this);

                // 드롭 위치를 타임라인 시간과 트랙으로 변환
                long startTimeMs = XToTime(dropPoint.X);
                int trackIndex = (int)((dropPoint.Y - TimelineHeight) / TrackHeight);
                trackIndex = Math.Clamp(trackIndex, 0, 4); // 0-4 트랙

                // ViewModel을 통해 클립 추가
                Dispatcher.UIThread.Post(() =>
                {
                    ViewModel.AddClipFromMediaItem(mediaItem, startTimeMs, trackIndex);
                });
            }

            e.Handled = true;
        }
    }
}
