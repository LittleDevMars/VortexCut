using System.Collections.ObjectModel;
using VortexCut.Core.Interfaces;
using VortexCut.Core.Models;

namespace VortexCut.UI.Services.Actions;

/// <summary>
/// 자막 클립 추가 액션 (C#만, Rust FFI 불필요)
/// </summary>
public class AddSubtitleClipAction : IUndoableAction
{
    private readonly ObservableCollection<ClipModel> _clips;
    private readonly SubtitleClipModel _clip;

    public string Description => "자막 추가";

    public AddSubtitleClipAction(ObservableCollection<ClipModel> clips, SubtitleClipModel clip)
    {
        _clips = clips;
        _clip = clip;
    }

    public void Execute() => _clips.Add(_clip);
    public void Undo() => _clips.Remove(_clip);
}

/// <summary>
/// 자막 클립 삭제 액션
/// </summary>
public class DeleteSubtitleClipAction : IUndoableAction
{
    private readonly ObservableCollection<ClipModel> _clips;
    private readonly SubtitleClipModel _clip;

    // 복원용 스냅샷
    private readonly string _text;
    private readonly SubtitleStyle _style;
    private readonly long _startTimeMs;
    private readonly long _durationMs;
    private readonly int _trackIndex;

    public string Description => "자막 삭제";

    public DeleteSubtitleClipAction(ObservableCollection<ClipModel> clips, SubtitleClipModel clip)
    {
        _clips = clips;
        _clip = clip;
        _text = clip.Text;
        _style = clip.Style.Clone();
        _startTimeMs = clip.StartTimeMs;
        _durationMs = clip.DurationMs;
        _trackIndex = clip.TrackIndex;
    }

    public void Execute() => _clips.Remove(_clip);

    public void Undo()
    {
        // 원본 데이터 복원
        _clip.Text = _text;
        _clip.Style = _style;
        _clip.StartTimeMs = _startTimeMs;
        _clip.DurationMs = _durationMs;
        _clip.TrackIndex = _trackIndex;
        _clips.Add(_clip);
    }
}

/// <summary>
/// 자막 텍스트 편집 액션
/// </summary>
public class EditSubtitleTextAction : IUndoableAction
{
    private readonly SubtitleClipModel _clip;
    private readonly string _oldText;
    private readonly string _newText;

    public string Description => "자막 편집";

    public EditSubtitleTextAction(SubtitleClipModel clip, string oldText, string newText)
    {
        _clip = clip;
        _oldText = oldText;
        _newText = newText;
    }

    public void Execute() => _clip.Text = _newText;
    public void Undo() => _clip.Text = _oldText;
}
