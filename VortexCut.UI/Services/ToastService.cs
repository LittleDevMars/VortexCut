using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using VortexCut.UI.Controls;

namespace VortexCut.UI.Services;

/// <summary>
/// Toast 알림을 관리하는 서비스
/// </summary>
public class ToastService
{
    private Grid? _container;
    private readonly List<ToastNotification> _activeToasts = new();
    private const int MaxToasts = 5;
    private const int ToastSpacing = 12;

    public void Initialize(Grid container)
    {
        _container = container;
        System.Diagnostics.Debug.WriteLine($"ToastService initialized with container: {container?.GetType().Name}");
    }

    public void ShowSuccess(string title, string message = "", int durationMs = 4000)
    {
        Show(title, message, ToastType.Success, durationMs);
    }

    public void ShowError(string title, string message = "", int durationMs = 6000)
    {
        Show(title, message, ToastType.Error, durationMs);
    }

    public void ShowWarning(string title, string message = "", int durationMs = 5000)
    {
        Show(title, message, ToastType.Warning, durationMs);
    }

    public void ShowInfo(string title, string message = "", int durationMs = 4000)
    {
        Show(title, message, ToastType.Info, durationMs);
    }

    private void Show(string title, string message, ToastType type, int durationMs)
    {
        System.Diagnostics.Debug.WriteLine($"🔔 Toast requested: {title} - {message} ({type})");

        if (_container == null)
        {
            System.Diagnostics.Debug.WriteLine("❌ ToastService: Container not initialized");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"✅ Container found, showing toast...");

        Dispatcher.UIThread.Post(() =>
        {
            // 최대 개수 초과 시 가장 오래된 것 제거
            if (_activeToasts.Count >= MaxToasts)
            {
                var oldest = _activeToasts[0];
                RemoveToast(oldest);
            }

            var toast = new ToastNotification();
            toast.Margin = new Avalonia.Thickness(0, 0, 16, ToastSpacing);
            toast.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            toast.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;

            toast.Closed += (s, e) => RemoveToast(toast);

            _container.Children.Add(toast);
            _activeToasts.Add(toast);

            toast.Show(title, message, type, durationMs);

            // 위치 재조정
            UpdateToastPositions();
        });
    }

    private void RemoveToast(ToastNotification toast)
    {
        if (_container == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            _activeToasts.Remove(toast);
            _container.Children.Remove(toast);
            UpdateToastPositions();
        });
    }

    private void UpdateToastPositions()
    {
        // 하단에서 위로 쌓이도록 위치 조정
        double bottomOffset = 0;

        for (int i = _activeToasts.Count - 1; i >= 0; i--)
        {
            var toast = _activeToasts[i];
            toast.Margin = new Avalonia.Thickness(0, 0, 16, bottomOffset + ToastSpacing);
            bottomOffset += toast.Bounds.Height + ToastSpacing;
        }
    }
}
