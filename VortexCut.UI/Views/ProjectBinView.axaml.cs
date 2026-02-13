using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VortexCut.Core.Models;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Views;

public partial class ProjectBinView : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragging;

    public ProjectBinView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // ListBox 더블클릭 → Clip Monitor에 로드
        var listBox = this.FindControl<ListBox>("MediaListBox");
        if (listBox != null)
        {
            listBox.DoubleTapped += OnMediaItemDoubleTapped;
        }
    }

    /// <summary>
    /// Project Bin 미디어 아이템 더블클릭 → Clip Monitor에 로드
    /// </summary>
    private void OnMediaItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        var listBox = sender as ListBox;
        if (listBox?.SelectedItem is not MediaItem mediaItem) return;

        // MainViewModel을 찾아서 LoadClipToSourceMonitor 호출
        // ProjectBinView의 DataContext는 ProjectBinViewModel이므로
        // 상위 트리에서 MainViewModel을 가져옴
        var mainWindow = TopLevel.GetTopLevel(this);
        if (mainWindow?.DataContext is MainViewModel mainVm)
        {
            mainVm.LoadClipToSourceMonitor(mediaItem);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !_isDragging)
        {
            var currentPoint = e.GetPosition(this);
            var diff = currentPoint - _dragStartPoint;

            // 최소 드래그 거리 확인
            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                // ListBox에서 선택된 항목 가져오기
                var listBox = this.FindControl<ListBox>("MediaListBox");
                if (listBox?.SelectedItem is MediaItem mediaItem)
                {
                    _isDragging = true;
#pragma warning disable CS0618 // DataObject/DoDragDrop deprecated in 11.3
                    var dragData = new DataObject();
                    dragData.Set("MediaItem", mediaItem);
                    DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
#pragma warning restore CS0618
                    _isDragging = false;
                }
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
    }
}
