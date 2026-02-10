using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Controls.Timeline;

/// <summary>
/// 타임라인 미니맵 (전체 타임라인 축소 뷰)
/// </summary>
public class TimelineMinimap : Control
{
    private TimelineViewModel? _viewModel;
    private const double MinimapHeight = 20;
    private double _pixelsPerMs = 0.01; // 전체 타임라인을 맞추기 위한 스케일

    public TimelineMinimap()
    {
        Height = MinimapHeight;
    }

    public void SetViewModel(TimelineViewModel viewModel)
    {
        _viewModel = viewModel;

        // 클립 컬렉션 변경 시 갱신
        _viewModel.Clips.CollectionChanged += (s, e) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 배경 렌더링
        context.FillRectangle(
            new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            new Rect(0, 0, Bounds.Width, MinimapHeight));

        if (_viewModel == null || _viewModel.Clips.Count == 0)
            return;

        // 전체 타임라인 길이 계산
        var totalDuration = _viewModel.Clips
            .Select(c => c.EndTimeMs)
            .DefaultIfEmpty(0)
            .Max();

        if (totalDuration == 0)
            return;

        // 미니맵 스케일 계산 (전체 타임라인을 Bounds.Width에 맞춤)
        _pixelsPerMs = Bounds.Width / totalDuration;

        // 모든 클립 렌더링 (작은 박스)
        foreach (var clip in _viewModel.Clips)
        {
            var x = clip.StartTimeMs * _pixelsPerMs;
            var width = clip.DurationMs * _pixelsPerMs;
            var y = clip.TrackIndex * 2.0; // 트랙별로 2px씩 오프셋
            var height = 2.0;

            if (y + height > MinimapHeight)
                y = MinimapHeight - height;

            var rect = new Rect(x, y, Math.Max(width, 1), height);
            context.FillRectangle(Brushes.Gray, rect);
        }

        // 현재 뷰포트 하이라이트 (노란 테두리)
        var viewportRect = CalculateViewportRect();
        context.DrawRectangle(
            new Pen(Brushes.Yellow, 2),
            viewportRect);

        // Playhead 표시 (빨간 선)
        var playheadX = _viewModel.CurrentTimeMs * _pixelsPerMs;
        context.DrawLine(
            new Pen(Brushes.Red, 1),
            new Point(playheadX, 0),
            new Point(playheadX, MinimapHeight));
    }

    /// <summary>
    /// 현재 뷰포트 사각형 계산
    /// </summary>
    private Rect CalculateViewportRect()
    {
        if (_viewModel == null)
            return new Rect();

        // TODO: 실제 스크롤 오프셋과 줌 레벨을 사용해서 계산
        // 현재는 전체 뷰포트로 가정
        var viewportWidth = Bounds.Width * 0.3; // 전체의 30%
        var viewportX = _viewModel.CurrentTimeMs * _pixelsPerMs - viewportWidth / 2;
        viewportX = Math.Clamp(viewportX, 0, Bounds.Width - viewportWidth);

        return new Rect(viewportX, 0, viewportWidth, MinimapHeight);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_viewModel == null)
            return;

        // 클릭 위치로 Playhead 이동
        var clickX = e.GetPosition(this).X;
        var timeMs = (long)(clickX / _pixelsPerMs);

        // 타임라인 총 길이 내로 제한
        var maxTime = _viewModel.Clips
            .Select(c => c.EndTimeMs)
            .DefaultIfEmpty(0)
            .Max();

        _viewModel.CurrentTimeMs = Math.Clamp(timeMs, 0, maxTime);

        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        // 드래그 중이면 Playhead 계속 업데이트
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (_viewModel == null)
                return;

            var currentX = e.GetPosition(this).X;
            var timeMs = (long)(currentX / _pixelsPerMs);

            var maxTime = _viewModel.Clips
                .Select(c => c.EndTimeMs)
                .DefaultIfEmpty(0)
                .Max();

            _viewModel.CurrentTimeMs = Math.Clamp(timeMs, 0, maxTime);

            e.Handled = true;
            InvalidateVisual();
        }
    }
}
