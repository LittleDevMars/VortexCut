using VortexCut.Core.Models;
using VortexCut.UI.Services.Actions;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Services;

/// <summary>
/// Razor 도구 (클립 자르기) — Undo/Redo + Rust 동기화
/// </summary>
public class RazorTool
{
    private readonly TimelineViewModel _timeline;

    public RazorTool(TimelineViewModel timeline)
    {
        _timeline = timeline;
    }

    /// <summary>
    /// 특정 시간에 클립 자르기 (Undo 지원 + Rust 동기화)
    /// </summary>
    public void CutClipAtTime(ClipModel clip, long cutTimeMs)
    {
        // 자르기 위치가 클립 범위 밖이면 무시
        if (cutTimeMs <= clip.StartTimeMs || cutTimeMs >= clip.EndTimeMs)
            return;

        var action = new RazorSplitAction(
            _timeline.Clips, _timeline.ProjectServiceRef, clip, cutTimeMs);
        _timeline.UndoRedo.ExecuteAction(action);
    }

    /// <summary>
    /// 특정 시간에 모든 트랙의 클립 동시 자르기 (CompositeAction)
    /// </summary>
    public void CutAllTracksAtTime(long cutTimeMs)
    {
        var clipsToCut = _timeline.Clips
            .Where(c => c.StartTimeMs < cutTimeMs && c.EndTimeMs > cutTimeMs)
            .ToList();

        if (clipsToCut.Count == 0) return;

        if (clipsToCut.Count == 1)
        {
            CutClipAtTime(clipsToCut[0], cutTimeMs);
        }
        else
        {
            var actions = clipsToCut
                .Select(clip => new RazorSplitAction(
                    _timeline.Clips, _timeline.ProjectServiceRef, clip, cutTimeMs))
                .Cast<Core.Interfaces.IUndoableAction>()
                .ToList();
            var composite = new CompositeAction("전체 트랙 분할", actions);
            _timeline.UndoRedo.ExecuteAction(composite);
        }
    }
}
