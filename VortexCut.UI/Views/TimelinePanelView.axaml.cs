using Avalonia.Controls;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Views;

public partial class TimelinePanelView : UserControl
{
    public TimelinePanelView()
    {
        InitializeComponent();

        // DataContext가 변경되면 TimelineCanvas에 ViewModel 설정
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var canvas = this.FindControl<Controls.TimelineCanvas>("TimelineCanvas");
            if (canvas != null)
            {
                canvas.ViewModel = viewModel.Timeline;
                System.Diagnostics.Debug.WriteLine("✅ TimelineCanvas ViewModel set successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ TimelineCanvas not found!");
            }
        }
    }
}
