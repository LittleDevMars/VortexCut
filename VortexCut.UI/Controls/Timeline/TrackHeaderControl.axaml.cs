using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VortexCut.Core.Models;

namespace VortexCut.UI.Controls.Timeline;

/// <summary>
/// 개별 트랙 헤더 컨트롤
/// </summary>
public partial class TrackHeaderControl : UserControl
{
    public static readonly StyledProperty<TrackModel?> TrackProperty =
        AvaloniaProperty.Register<TrackHeaderControl, TrackModel?>(nameof(Track));

    public TrackModel? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    private Border? _resizeGrip;
    private bool _isResizing;
    private Point _resizeStartPoint;
    private double _originalHeight;

    public TrackHeaderControl()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _resizeGrip = this.FindControl<Border>("ResizeGrip");

        if (_resizeGrip != null)
        {
            _resizeGrip.PointerPressed += OnResizeGripPressed;
            _resizeGrip.PointerMoved += OnResizeGripMoved;
            _resizeGrip.PointerReleased += OnResizeGripReleased;
        }
    }

    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Track == null) return;

        _isResizing = true;
        _resizeStartPoint = e.GetPosition(this);
        _originalHeight = Track.Height;
        e.Handled = true;
    }

    private void OnResizeGripMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing || Track == null) return;

        var currentPoint = e.GetPosition(this);
        var deltaY = currentPoint.Y - _resizeStartPoint.Y;

        // 트랙 높이 조절 (30~200px)
        Track.Height = Math.Clamp(_originalHeight + deltaY, 30, 200);
        e.Handled = true;
    }

    private void OnResizeGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isResizing = false;
        e.Handled = true;
    }
}
