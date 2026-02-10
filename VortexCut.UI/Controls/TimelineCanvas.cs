using Avalonia.Controls;
using VortexCut.Core.Models;
using VortexCut.UI.Controls.Timeline;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Controls;

/// <summary>
/// Kdenlive 스타일 타임라인 캔버스 (Grid 컨테이너)
/// </summary>
public class TimelineCanvas : Grid
{
    private readonly TimelineHeader _timelineHeader;
    private readonly TrackListPanel _trackListPanel;
    private readonly ClipCanvasPanel _clipCanvasPanel;
    private readonly ScrollViewer _scrollViewer;

    private TimelineViewModel? _viewModel;
    private double _pixelsPerMs = 0.1;
    private double _scrollOffsetX = 0;

    /// <summary>
    /// ViewModel 참조
    /// </summary>
    public TimelineViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            if (_viewModel != null)
            {
                _timelineHeader.SetViewModel(_viewModel);
                _clipCanvasPanel.SetViewModel(_viewModel);
                _trackListPanel.SetTracks(_viewModel.VideoTracks, _viewModel.AudioTracks);

                // 트랙/클립 변경 감지
                _viewModel.VideoTracks.CollectionChanged += (s, e) => UpdateTracks();
                _viewModel.AudioTracks.CollectionChanged += (s, e) => UpdateTracks();
                _viewModel.Clips.CollectionChanged += (s, e) => UpdateClips();
            }
        }
    }

    public TimelineCanvas()
    {
        // Grid 레이아웃 설정
        RowDefinitions = new RowDefinitions("80,*"); // Header 80px, 나머지는 트랙
        ColumnDefinitions = new ColumnDefinitions("60,*"); // 트랙 헤더 60px, 나머지는 클립

        // 컴포넌트 생성
        _timelineHeader = new TimelineHeader();
        _trackListPanel = new TrackListPanel();
        _clipCanvasPanel = new ClipCanvasPanel();

        // ScrollViewer (수평/수직 스크롤)
        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _clipCanvasPanel
        };

        // Grid 배치
        // Row 0: TimelineHeader (전체 컬럼 병합)
        _timelineHeader.SetValue(ColumnSpanProperty, 2);
        _timelineHeader.SetValue(RowProperty, 0);

        // Row 1, Column 0: TrackListPanel
        _trackListPanel.SetValue(RowProperty, 1);
        _trackListPanel.SetValue(ColumnProperty, 0);

        // Row 1, Column 1: ClipCanvasPanel (ScrollViewer로 감싸기)
        _scrollViewer.SetValue(RowProperty, 1);
        _scrollViewer.SetValue(ColumnProperty, 1);

        Children.Add(_timelineHeader);
        Children.Add(_trackListPanel);
        Children.Add(_scrollViewer);

        // 스크롤 이벤트 연결
        _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    /// <summary>
    /// Zoom 레벨 설정 (Ctrl + 마우스휠)
    /// </summary>
    public void SetZoom(double pixelsPerMs)
    {
        _pixelsPerMs = Math.Clamp(pixelsPerMs, 0.01, 1.0);
        _timelineHeader.SetZoom(_pixelsPerMs);
        _clipCanvasPanel.SetZoom(_pixelsPerMs);
    }

    /// <summary>
    /// 스크롤 변경 이벤트
    /// </summary>
    private void OnScrollChanged(object? sender, Avalonia.Controls.ScrollChangedEventArgs e)
    {
        if (_scrollViewer != null)
        {
            _scrollOffsetX = _scrollViewer.Offset.X;
            _timelineHeader.SetScrollOffset(_scrollOffsetX);
            _clipCanvasPanel.SetScrollOffset(_scrollOffsetX);
        }
    }

    /// <summary>
    /// 트랙 업데이트
    /// </summary>
    private void UpdateTracks()
    {
        if (_viewModel == null) return;

        _trackListPanel.SetTracks(_viewModel.VideoTracks, _viewModel.AudioTracks);
        _clipCanvasPanel.SetTracks(_viewModel.VideoTracks.ToList(), _viewModel.AudioTracks.ToList());
    }

    /// <summary>
    /// 클립 업데이트
    /// </summary>
    private void UpdateClips()
    {
        if (_viewModel == null) return;

        _clipCanvasPanel.SetClips(_viewModel.Clips);
    }

}
