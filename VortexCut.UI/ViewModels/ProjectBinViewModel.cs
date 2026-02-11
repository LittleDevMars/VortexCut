using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VortexCut.Core.Models;

namespace VortexCut.UI.ViewModels;

/// <summary>
/// Project Bin (미디어 라이브러리) ViewModel
/// </summary>
public partial class ProjectBinViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<MediaItem> _mediaItems = new();

    [ObservableProperty]
    private MediaItem? _selectedItem;

    [ObservableProperty]
    private bool _isLoading = false;

    /// <summary>
    /// 빈 상태 여부 (Empty State 표시용)
    /// </summary>
    public bool IsEmpty => MediaItems.Count == 0 && !IsLoading;

    /// <summary>
    /// 미디어 아이템 추가
    /// </summary>
    public void AddMediaItem(MediaItem item)
    {
        MediaItems.Add(item);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// 미디어 아이템 제거
    /// </summary>
    [RelayCommand]
    private void RemoveSelectedItem()
    {
        if (SelectedItem != null)
        {
            MediaItems.Remove(SelectedItem);
            SelectedItem = null;
        }
    }

    /// <summary>
    /// 모든 아이템 제거
    /// </summary>
    public void Clear()
    {
        MediaItems.Clear();
        SelectedItem = null;
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// 로딩 상태 설정 (미디어 임포트 시작/종료)
    /// </summary>
    public void SetLoading(bool loading)
    {
        IsLoading = loading;
        OnPropertyChanged(nameof(IsEmpty));
    }
}
