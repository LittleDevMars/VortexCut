using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.ObjectModel;
using VortexCut.Core.Models;

namespace VortexCut.UI.Controls.Timeline;

/// <summary>
/// 트랙 헤더 목록 패널 (왼쪽 60px 고정폭)
/// </summary>
public class TrackListPanel : StackPanel
{
    private ObservableCollection<TrackModel>? _videoTracks;
    private ObservableCollection<TrackModel>? _audioTracks;

    public TrackListPanel()
    {
        Orientation = Avalonia.Layout.Orientation.Vertical;
        Width = 60;
    }

    public void SetTracks(ObservableCollection<TrackModel> videoTracks, ObservableCollection<TrackModel> audioTracks)
    {
        _videoTracks = videoTracks;
        _audioTracks = audioTracks;

        RebuildHeaders();

        // 트랙 변경 감지
        videoTracks.CollectionChanged += (s, e) => RebuildHeaders();
        audioTracks.CollectionChanged += (s, e) => RebuildHeaders();
    }

    private void RebuildHeaders()
    {
        Children.Clear();

        if (_videoTracks != null)
        {
            foreach (var track in _videoTracks)
            {
                var header = new TrackHeaderControl
                {
                    Track = track,
                    Height = track.Height
                };
                Children.Add(header);
            }
        }

        if (_audioTracks != null)
        {
            foreach (var track in _audioTracks)
            {
                var header = new TrackHeaderControl
                {
                    Track = track,
                    Height = track.Height
                };
                Children.Add(header);
            }
        }
    }
}
