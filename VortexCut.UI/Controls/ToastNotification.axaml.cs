using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace VortexCut.UI.Controls;

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public partial class ToastNotification : UserControl
{
    private DispatcherTimer? _autoCloseTimer;

    public event EventHandler? Closed;

    public ToastNotification()
    {
        InitializeComponent();

        var closeButton = this.FindControl<Button>("CloseButton");
        if (closeButton != null)
        {
            closeButton.Click += OnCloseButtonClick;
        }
    }

    public void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 4000)
    {
        var border = this.FindControl<Border>("ToastBorder");
        var iconText = this.FindControl<TextBlock>("IconText");
        var titleText = this.FindControl<TextBlock>("TitleText");
        var messageText = this.FindControl<TextBlock>("MessageText");

        if (border == null || iconText == null || titleText == null || messageText == null)
            return;

        // 타입에 따른 스타일 적용
        border.Classes.Clear();
        border.Classes.Add("toast");

        switch (type)
        {
            case ToastType.Success:
                border.Classes.Add("success");
                iconText.Text = "✓";
                iconText.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
                break;
            case ToastType.Error:
                border.Classes.Add("error");
                iconText.Text = "✕";
                iconText.Foreground = new SolidColorBrush(Color.Parse("#F48771"));
                break;
            case ToastType.Warning:
                border.Classes.Add("warning");
                iconText.Text = "⚠";
                iconText.Foreground = new SolidColorBrush(Color.Parse("#D7BA7D"));
                break;
            case ToastType.Info:
                border.Classes.Add("info");
                iconText.Text = "ℹ";
                iconText.Foreground = new SolidColorBrush(Color.Parse("#007ACC"));
                break;
        }

        titleText.Text = title;
        messageText.Text = message;
        messageText.IsVisible = !string.IsNullOrEmpty(message);

        // Fade In 애니메이션
        this.Opacity = 0;
        this.RenderTransform = new TranslateTransform(0, 20);

        Dispatcher.UIThread.Post(() =>
        {
            this.Opacity = 1;
            this.RenderTransform = new TranslateTransform(0, 0);
        }, DispatcherPriority.Render);

        // Auto Close 타이머
        if (durationMs > 0)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            _autoCloseTimer.Tick += (s, e) => Close();
            _autoCloseTimer.Start();
        }
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Close()
    {
        _autoCloseTimer?.Stop();

        // Fade Out 애니메이션
        this.Opacity = 0;
        this.RenderTransform = new TranslateTransform(0, -20);

        // 애니메이션 완료 후 제거
        DispatcherTimer closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        closeTimer.Tick += (s, e) =>
        {
            closeTimer.Stop();
            Closed?.Invoke(this, EventArgs.Empty);
        };
        closeTimer.Start();
    }
}
