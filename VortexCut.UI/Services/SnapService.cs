using VortexCut.Core.Models;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Services;

/// <summary>
/// Snap 결과
/// </summary>
public record SnapResult(long TimeMs, bool Snapped);

/// <summary>
/// 타임라인 Snap 서비스 (클립 끝/마커/Playhead에 자동 정렬)
/// </summary>
public class SnapService
{
    private readonly TimelineViewModel _timeline;

    public SnapService(TimelineViewModel timeline)
    {
        _timeline = timeline;
    }

    /// <summary>
    /// 주어진 시간에 대한 Snap 타겟을 찾습니다
    /// </summary>
    /// <param name="timeMs">원래 시간 (ms)</param>
    /// <param name="excludeClip">제외할 클립 (드래그 중인 클립)</param>
    /// <returns>Snap 결과 (조정된 시간, Snap 여부)</returns>
    public SnapResult GetSnapTarget(long timeMs, ClipModel? excludeClip = null)
    {
        if (!_timeline.SnapEnabled)
            return new SnapResult(timeMs, false);

        var candidates = new List<long>();

        // 1. Playhead (현재 재생 위치)
        candidates.Add(_timeline.CurrentTimeMs);

        // 2. 모든 클립의 시작/끝
        foreach (var clip in _timeline.Clips.Where(c => c != excludeClip))
        {
            candidates.Add(clip.StartTimeMs);
            candidates.Add(clip.EndTimeMs);
        }

        // 3. 마커 (Phase 2D에서 추가 예정)
        // foreach (var marker in _timeline.Markers)
        // {
        //     candidates.Add(marker.TimeMs);
        // }

        // 후보가 없으면 원래 시간 반환
        if (candidates.Count == 0)
            return new SnapResult(timeMs, false);

        // 가장 가까운 후보 찾기
        var closest = candidates
            .OrderBy(c => Math.Abs(c - timeMs))
            .First();

        // Snap 임계값 내에 있으면 Snap
        if (Math.Abs(closest - timeMs) <= _timeline.SnapThresholdMs)
        {
            return new SnapResult(closest, true);
        }

        return new SnapResult(timeMs, false);
    }

    /// <summary>
    /// 여러 Snap 포인트 가져오기 (가이드라인 렌더링용)
    /// </summary>
    public List<long> GetAllSnapPoints(ClipModel? excludeClip = null)
    {
        var snapPoints = new List<long>();

        // Playhead
        snapPoints.Add(_timeline.CurrentTimeMs);

        // 모든 클립의 시작/끝
        foreach (var clip in _timeline.Clips.Where(c => c != excludeClip))
        {
            snapPoints.Add(clip.StartTimeMs);
            snapPoints.Add(clip.EndTimeMs);
        }

        // 마커 (Phase 2D)
        // foreach (var marker in _timeline.Markers)
        // {
        //     snapPoints.Add(marker.TimeMs);
        // }

        return snapPoints.Distinct().OrderBy(t => t).ToList();
    }
}
