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

    /// <summary>
    /// 미디어 아이템 추가
    /// </summary>
    public void AddMediaItem(MediaItem item)
    {
        MediaItems.Add(item);
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
    }
}
