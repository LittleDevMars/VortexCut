using Avalonia.Controls;
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

        // StorageProvider 설정
        Opened += (sender, e) =>
        {
            _viewModel.SetStorageProvider(StorageProvider);
            InitializeTimeline();
        };
    }

    private void InitializeTimeline()
    {
        if (_viewModel == null) return;

        // Timeline Canvas 업데이트
        _viewModel.Timeline.Clips.CollectionChanged += (s, e) =>
        {
            UpdateTimelineCanvas();
        };

        UpdateTimelineCanvas();
    }

    private void UpdateTimelineCanvas()
    {
        if (_viewModel == null) return;

        // TimelineCanvas에 클립 데이터 전달
        var canvas = this.FindControl<Controls.TimelineCanvas>("TimelineCanvas");
        if (canvas != null)
        {
            canvas.SetClips(_viewModel.Timeline.Clips);
        }
    }
}
