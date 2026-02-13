using System.Collections.ObjectModel;
using VortexCut.Core.Interfaces;
using VortexCut.Core.Models;

namespace VortexCut.UI.Services.Actions;

/// <summary>
/// 트랙 추가 액션
/// </summary>
public class AddTrackAction : IUndoableAction
{
    private readonly ObservableCollection<TrackModel> _tracks;
    private readonly TrackModel _track;

    public string Description => $"트랙 추가 ({_track.Name})";

    public AddTrackAction(ObservableCollection<TrackModel> tracks, TrackModel track)
    {
        _tracks = tracks;
        _track = track;
    }

    public void Execute()
    {
        _tracks.Add(_track);
    }

    public void Undo()
    {
        _tracks.Remove(_track);
    }
}

/// <summary>
/// 트랙 제거 액션 (소속 클립 포함)
/// </summary>
public class RemoveTrackAction : IUndoableAction
{
    private readonly ObservableCollection<TrackModel> _tracks;
    private readonly ObservableCollection<ClipModel> _clips;
    private readonly TrackModel _track;
    private readonly int _trackPosition;
    private readonly List<ClipModel> _removedClips;

    public string Description => $"트랙 삭제 ({_track.Name})";

    public RemoveTrackAction(
        ObservableCollection<TrackModel> tracks,
        ObservableCollection<ClipModel> clips,
        TrackModel track)
    {
        _tracks = tracks;
        _clips = clips;
        _track = track;
        _trackPosition = tracks.IndexOf(track);

        // 소속 클립 스냅샷
        _removedClips = clips.Where(c => c.TrackIndex == track.Index).ToList();
    }

    public void Execute()
    {
        // 소속 클립 먼저 제거
        foreach (var clip in _removedClips)
            _clips.Remove(clip);

        _tracks.Remove(_track);

        // 인덱스 재정렬
        for (int i = 0; i < _tracks.Count; i++)
            _tracks[i].Index = i;
    }

    public void Undo()
    {
        // 트랙 원래 위치에 삽입
        if (_trackPosition < _tracks.Count)
            _tracks.Insert(_trackPosition, _track);
        else
            _tracks.Add(_track);

        // 인덱스 재정렬
        for (int i = 0; i < _tracks.Count; i++)
            _tracks[i].Index = i;

        // 소속 클립 복원
        foreach (var clip in _removedClips)
            _clips.Add(clip);
    }
}
