using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using VortexCut.Core.Models;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Controls.Timeline;

/// <summary>
/// 클립 렌더링 영역 (드래그, 선택, Snap 처리)
/// </summary>
public class ClipCanvasPanel : Control
{
    private const double TrackHeight = 60;
    private const double MinClipWidth = 20;

    private TimelineViewModel? _viewModel;
    private List<ClipModel> _clips = new();
    private List<TrackModel> _videoTracks = new();
    private List<TrackModel> _audioTracks = new();
    private double _pixelsPerMs = 0.1;
    private double _scrollOffsetX = 0;
    private ClipModel? _selectedClip;
    private ClipModel? _draggingClip;
    private Point _dragStartPoint;
    private bool _isDragging;

    public ClipCanvasPanel()
    {
        ClipToBounds = true;
        Focusable = true;
        DragDrop.SetAllowDrop(this, true);

        AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    public void SetViewModel(TimelineViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void SetClips(IEnumerable<ClipModel> clips)
    {
        _clips = new List<ClipModel>(clips);
        InvalidateVisual();
    }

    public void SetTracks(List<TrackModel> videoTracks, List<TrackModel> audioTracks)
    {
        _videoTracks = videoTracks;
        _audioTracks = audioTracks;
        InvalidateVisual();
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
        context.FillRectangle(new SolidColorBrush(Color.Parse("#1E1E1E")), Bounds);

        // 트랙 배경
        DrawTrackBackgrounds(context);

        // 클립들
        DrawClips(context);

        // Playhead
        DrawPlayhead(context);
    }

    private void DrawTrackBackgrounds(DrawingContext context)
    {
        var trackBrush = new SolidColorBrush(Color.Parse("#2D2D30"));
        var borderPen = new Pen(new SolidColorBrush(Color.Parse("#444444")), 1);

        // 비디오 트랙
        for (int i = 0; i < _videoTracks.Count; i++)
        {
            var track = _videoTracks[i];
            double y = i * track.Height;
            var trackRect = new Rect(0, y, Bounds.Width, track.Height);

            context.FillRectangle(trackBrush, trackRect);
            context.DrawRectangle(borderPen, trackRect);
        }

        // 오디오 트랙
        double audioStartY = _videoTracks.Sum(t => t.Height);
        for (int i = 0; i < _audioTracks.Count; i++)
        {
            var track = _audioTracks[i];
            double y = audioStartY + i * track.Height;
            var trackRect = new Rect(0, y, Bounds.Width, track.Height);

            context.FillRectangle(trackBrush, trackRect);
            context.DrawRectangle(borderPen, trackRect);
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

        // 트랙 Y 위치 계산
        double y = GetTrackYPosition(clip.TrackIndex);
        var track = GetTrackByIndex(clip.TrackIndex);
        if (track == null) return;

        double height = track.Height - 10;
        var clipRect = new Rect(x, y + 5, Math.Max(width, MinClipWidth), height);

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

        context.DrawText(text, new Point(x + 5, y + 10));
    }

    private void DrawPlayhead(DrawingContext context)
    {
        if (_viewModel == null) return;

        double x = TimeToX(_viewModel.CurrentTimeMs);
        var pen = new Pen(Brushes.Red, 2);
        context.DrawLine(pen,
            new Point(x, 0),
            new Point(x, Bounds.Height));
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
            double y = GetTrackYPosition(clip.TrackIndex);
            var track = GetTrackByIndex(clip.TrackIndex);
            if (track == null) continue;

            double height = track.Height - 10;
            var clipRect = new Rect(x, y + 5, Math.Max(width, MinClipWidth), height);

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
        if (e.Data.Contains("MediaItem") && _viewModel != null)
        {
            var mediaItem = e.Data.Get("MediaItem") as MediaItem;
            if (mediaItem != null)
            {
                var dropPoint = e.GetPosition(this);

                // 드롭 위치를 타임라인 시간과 트랙으로 변환
                long startTimeMs = XToTime(dropPoint.X);
                int trackIndex = GetTrackIndexAtY(dropPoint.Y);

                // ViewModel을 통해 클립 추가
                Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.AddClipFromMediaItem(mediaItem, startTimeMs, trackIndex);
                });
            }

            e.Handled = true;
        }
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

    private double GetTrackYPosition(int trackIndex)
    {
        double y = 0;
        for (int i = 0; i < trackIndex && i < _videoTracks.Count; i++)
        {
            y += _videoTracks[i].Height;
        }
        return y;
    }

    private TrackModel? GetTrackByIndex(int index)
    {
        if (index < _videoTracks.Count)
            return _videoTracks[index];

        int audioIndex = index - _videoTracks.Count;
        if (audioIndex >= 0 && audioIndex < _audioTracks.Count)
            return _audioTracks[audioIndex];

        return null;
    }

    private int GetTrackIndexAtY(double y)
    {
        double currentY = 0;

        // 비디오 트랙 검사
        for (int i = 0; i < _videoTracks.Count; i++)
        {
            if (y >= currentY && y < currentY + _videoTracks[i].Height)
                return i;
            currentY += _videoTracks[i].Height;
        }

        // 오디오 트랙 검사
        for (int i = 0; i < _audioTracks.Count; i++)
        {
            if (y >= currentY && y < currentY + _audioTracks[i].Height)
                return _videoTracks.Count + i;
            currentY += _audioTracks[i].Height;
        }

        return 0; // 기본값
    }
}
