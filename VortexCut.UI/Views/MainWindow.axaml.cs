using Avalonia.Controls;
using Avalonia.Input;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // 키보드 단축키
        KeyDown += OnKeyDown;

        // StorageProvider 설정 및 초기화
        Opened += (sender, e) =>
        {
            _viewModel.SetStorageProvider(StorageProvider);
            _viewModel.Initialize(); // 첫 프로젝트 생성
            InitializeTimeline();
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.Key)
        {
            case Key.K:
                // 키프레임 추가 (현재 Playhead 위치)
                _viewModel.Timeline.AddKeyframeAtCurrentTime();
                e.Handled = true;
                break;

            case Key.M:
                // 마커 추가 (현재 Playhead 위치)
                _viewModel.Timeline.AddMarker(
                    _viewModel.Timeline.CurrentTimeMs,
                    $"Marker {_viewModel.Timeline.Markers.Count + 1}");
                e.Handled = true;
                break;
        }
    }

    private void InitializeTimeline()
    {
        if (_viewModel == null) return;

        // TimelineCanvas에 ViewModel 설정
        var canvas = this.FindControl<Controls.TimelineCanvas>("TimelineCanvas");
        if (canvas != null)
        {
            canvas.ViewModel = _viewModel.Timeline;
        }
    }
}
