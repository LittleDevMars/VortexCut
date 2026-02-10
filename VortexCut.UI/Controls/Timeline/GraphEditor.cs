using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VortexCut.Core.Models;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Controls.Timeline;

/// <summary>
/// After Effects 스타일 그래프 에디터 (Value Graph / Speed Graph)
/// </summary>
public class GraphEditor : Control
{
    private TimelineViewModel? _viewModel;
    private ClipModel? _selectedClip;
    private KeyframeSystem? _keyframeSystem;

    // 뷰포트 변환
    private double _zoomX = 100; // 픽셀/초
    private double _zoomY = 100; // 픽셀/단위
    private double _panX = 0;
    private double _panY = 0;

    // 드래그 상태
    private Keyframe? _selectedKeyframe;
    private BezierHandle? _selectedHandle;
    private Keyframe? _selectedHandleKeyframe;
    private bool _isDraggingKeyframe;
    private bool _isDraggingHandle;
    private bool _isPanning;
    private Point _dragStartPoint;

    // 그리드 설정
    private const double GridSpacing = 50;

    public GraphEditor()
    {
        ClipToBounds = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel mainViewModel)
        {
            _viewModel = mainViewModel.Timeline;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TimelineViewModel.SelectedClips) ||
                    e.PropertyName == nameof(TimelineViewModel.SelectedKeyframeSystem))
                {
                    UpdateSelectedClipAndKeyframeSystem();
                    InvalidateVisual();
                }
            };
            UpdateSelectedClipAndKeyframeSystem();
        }
    }

    private void UpdateSelectedClipAndKeyframeSystem()
    {
        if (_viewModel == null) return;

        _selectedClip = _viewModel.SelectedClips.FirstOrDefault();
        if (_selectedClip != null)
        {
            _keyframeSystem = GetKeyframeSystem(_selectedClip, _viewModel.SelectedKeyframeSystem);
        }
        else
        {
            _keyframeSystem = null;
        }
    }

    private KeyframeSystem? GetKeyframeSystem(ClipModel clip, KeyframeSystemType type)
    {
        return type switch
        {
            KeyframeSystemType.Opacity => clip.OpacityKeyframes,
            KeyframeSystemType.Volume => clip.VolumeKeyframes,
            KeyframeSystemType.PositionX => clip.PositionXKeyframes,
            KeyframeSystemType.PositionY => clip.PositionYKeyframes,
            KeyframeSystemType.Scale => clip.ScaleKeyframes,
            KeyframeSystemType.Rotation => clip.RotationKeyframes,
            _ => null
        };
    }

    // 좌표 변환
    private Point ModelToScreen(double time, double value)
    {
        double x = (time * _zoomX) - _panX + Bounds.Width / 2;
        double y = Bounds.Height / 2 - (value * _zoomY) + _panY;
        return new Point(x, y);
    }

    private (double time, double value) ScreenToModel(Point screen)
    {
        double time = (screen.X - Bounds.Width / 2 + _panX) / _zoomX;
        double value = (Bounds.Height / 2 - screen.Y + _panY) / _zoomY;
        return (time, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 1. 배경
        context.FillRectangle(new SolidColorBrush(Color.Parse("#1E1E1E")), Bounds);

        // 2. 그리드
        DrawGrid(context);

        // 3. 축 (X축, Y축)
        DrawAxes(context);

        // 4. 키프레임 곡선 (샘플링)
        if (_keyframeSystem != null && _keyframeSystem.Keyframes.Count > 0)
        {
            DrawKeyframeCurve(context);
        }

        // 5. 키프레임 포인트
        if (_keyframeSystem != null && _keyframeSystem.Keyframes.Count > 0)
        {
            DrawKeyframePoints(context);
        }

        // 6. 베지어 핸들
        if (_selectedKeyframe != null)
        {
            DrawBezierHandles(context, _selectedKeyframe);
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);
        var centerPen = new Pen(new SolidColorBrush(Color.Parse("#404040")), 1.5);

        // 수평 그리드
        for (double y = 0; y < Bounds.Height; y += GridSpacing)
        {
            var pen = Math.Abs(y - Bounds.Height / 2) < 1 ? centerPen : gridPen;
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
        }

        // 수직 그리드
        for (double x = 0; x < Bounds.Width; x += GridSpacing)
        {
            var pen = Math.Abs(x - Bounds.Width / 2) < 1 ? centerPen : gridPen;
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }

    private void DrawAxes(DrawingContext context)
    {
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#606060")), 2);

        // X축 (시간)
        double y0 = ModelToScreen(0, 0).Y;
        if (y0 >= 0 && y0 <= Bounds.Height)
        {
            context.DrawLine(axisPen, new Point(0, y0), new Point(Bounds.Width, y0));
        }

        // Y축 (값)
        double x0 = ModelToScreen(0, 0).X;
        if (x0 >= 0 && x0 <= Bounds.Width)
        {
            context.DrawLine(axisPen, new Point(x0, 0), new Point(x0, Bounds.Height));
        }
    }

    private void DrawKeyframeCurve(DrawingContext context)
    {
        if (_keyframeSystem == null || _keyframeSystem.Keyframes.Count < 2) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            bool firstPoint = true;
            double startTime = _keyframeSystem.Keyframes.First().Time;
            double endTime = _keyframeSystem.Keyframes.Last().Time;

            // 0.01초 간격 샘플링
            for (double t = startTime; t <= endTime; t += 0.01)
            {
                double value = _keyframeSystem.Interpolate(t);
                var screenPoint = ModelToScreen(t, value);

                // 화면 밖은 스킵
                if (screenPoint.X < -100 || screenPoint.X > Bounds.Width + 100) continue;

                if (firstPoint)
                {
                    ctx.BeginFigure(screenPoint, false);
                    firstPoint = false;
                }
                else
                {
                    ctx.LineTo(screenPoint);
                }
            }
        }

        context.DrawGeometry(null, new Pen(Brushes.Cyan, 2), geometry);
    }

    private void DrawKeyframePoints(DrawingContext context)
    {
        if (_keyframeSystem == null) return;

        foreach (var keyframe in _keyframeSystem.Keyframes)
        {
            var centerPoint = ModelToScreen(keyframe.Time, keyframe.Value);

            // 화면 밖은 스킵
            if (centerPoint.X < -50 || centerPoint.X > Bounds.Width + 50) continue;

            var brush = keyframe == _selectedKeyframe ? Brushes.Yellow : Brushes.White;
            var radius = keyframe == _selectedKeyframe ? 6.0 : 4.0;

            context.DrawEllipse(brush, new Pen(Brushes.Black, 1.5), centerPoint, radius, radius);
        }
    }

    private void DrawBezierHandles(DrawingContext context, Keyframe keyframe)
    {
        var centerPoint = ModelToScreen(keyframe.Time, keyframe.Value);

        // OutHandle
        if (keyframe.OutHandle != null)
        {
            var handleTime = keyframe.Time + keyframe.OutHandle.TimeOffset;
            var handleValue = keyframe.Value + keyframe.OutHandle.ValueOffset;
            var handlePoint = ModelToScreen(handleTime, handleValue);

            context.DrawLine(new Pen(Brushes.Gray, 1.5), centerPoint, handlePoint);
            context.DrawEllipse(Brushes.Green, new Pen(Brushes.Black, 1), handlePoint, 5, 5);
        }

        // InHandle
        if (keyframe.InHandle != null)
        {
            var handleTime = keyframe.Time + keyframe.InHandle.TimeOffset;
            var handleValue = keyframe.Value + keyframe.InHandle.ValueOffset;
            var handlePoint = ModelToScreen(handleTime, handleValue);

            context.DrawLine(new Pen(Brushes.Gray, 1.5), centerPoint, handlePoint);
            context.DrawEllipse(Brushes.Red, new Pen(Brushes.Black, 1), handlePoint, 5, 5);
        }
    }

    // 마우스 이벤트
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _dragStartPoint = point;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            // 1. 베지어 핸들 우선
            var (handle, handleKeyframe) = GetHandleAtPosition(point, threshold: 10);
            if (handle != null && handleKeyframe != null)
            {
                _isDraggingHandle = true;
                _selectedHandle = handle;
                _selectedHandleKeyframe = handleKeyframe;
                _selectedKeyframe = handleKeyframe;
                _dragStartPoint = point;
                Cursor = new Cursor(StandardCursorType.Hand);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // 2. 키프레임 선택
            var clickedKeyframe = GetKeyframeAtPosition(point, threshold: 10);
            if (clickedKeyframe != null)
            {
                _isDraggingKeyframe = true;
                _selectedKeyframe = clickedKeyframe;
                _dragStartPoint = point;
                Cursor = new Cursor(StandardCursorType.Hand);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // 3. 빈 공간 클릭 - 선택 해제
            _selectedKeyframe = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        if (_isPanning)
        {
            var delta = point - _dragStartPoint;
            _panX -= delta.X;
            _panY -= delta.Y;
            _dragStartPoint = point;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingHandle && _selectedHandle != null && _selectedHandleKeyframe != null)
        {
            var (newTime, newValue) = ScreenToModel(point);
            _selectedHandle.TimeOffset = newTime - _selectedHandleKeyframe.Time;
            _selectedHandle.ValueOffset = newValue - _selectedHandleKeyframe.Value;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingKeyframe && _selectedKeyframe != null && _keyframeSystem != null)
        {
            var (newTime, newValue) = ScreenToModel(point);
            newTime = Math.Max(0, newTime);

            // 값 범위 제한 (0~100)
            newValue = Math.Clamp(newValue, 0, 100);

            _keyframeSystem.UpdateKeyframe(_selectedKeyframe, newTime, newValue);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = Cursor.Default;
            e.Handled = true;
        }

        if (_isDraggingHandle)
        {
            _isDraggingHandle = false;
            _selectedHandle = null;
            _selectedHandleKeyframe = null;
            Cursor = Cursor.Default;
            e.Handled = true;
        }

        if (_isDraggingKeyframe)
        {
            _isDraggingKeyframe = false;
            Cursor = Cursor.Default;
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var mousePos = e.GetPosition(this);
            var (mouseTime, mouseValue) = ScreenToModel(mousePos);

            var zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            _zoomX *= zoomFactor;
            _zoomY *= zoomFactor;

            // Zoom 후 마우스 위치 유지
            var newMouseScreen = ModelToScreen(mouseTime, mouseValue);
            _panX += (newMouseScreen.X - mousePos.X);
            _panY += (newMouseScreen.Y - mousePos.Y);

            InvalidateVisual();
            e.Handled = true;
        }
    }

    private Keyframe? GetKeyframeAtPosition(Point point, double threshold = 10)
    {
        if (_keyframeSystem == null) return null;

        foreach (var keyframe in _keyframeSystem.Keyframes)
        {
            var centerPoint = ModelToScreen(keyframe.Time, keyframe.Value);
            if (Distance(point, centerPoint) < threshold)
                return keyframe;
        }
        return null;
    }

    private (BezierHandle?, Keyframe?) GetHandleAtPosition(Point point, double threshold = 10)
    {
        if (_keyframeSystem == null) return (null, null);

        foreach (var keyframe in _keyframeSystem.Keyframes)
        {
            // OutHandle
            if (keyframe.OutHandle != null)
            {
                var handleTime = keyframe.Time + keyframe.OutHandle.TimeOffset;
                var handleValue = keyframe.Value + keyframe.OutHandle.ValueOffset;
                var handlePoint = ModelToScreen(handleTime, handleValue);

                if (Distance(point, handlePoint) < threshold)
                    return (keyframe.OutHandle, keyframe);
            }

            // InHandle
            if (keyframe.InHandle != null)
            {
                var handleTime = keyframe.Time + keyframe.InHandle.TimeOffset;
                var handleValue = keyframe.Value + keyframe.InHandle.ValueOffset;
                var handlePoint = ModelToScreen(handleTime, handleValue);

                if (Distance(point, handlePoint) < threshold)
                    return (keyframe.InHandle, keyframe);
            }
        }
        return (null, null);
    }

    private double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
