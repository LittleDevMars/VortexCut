using VortexCut.Core.Models;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Services;

/// <summary>
/// 리플 편집 서비스 (클립 삭제/이동 시 이후 클립 자동 조정)
/// </summary>
public class RippleEditService
{
    private readonly TimelineViewModel _timeline;

    public RippleEditService(TimelineViewModel timeline)
    {
        _timeline = timeline;
    }

    /// <summary>
    /// 리플 삭제: 클립 삭제 후 같은 트랙의 이후 클립들을 왼쪽으로 이동
    /// </summary>
    public void RippleDelete(ClipModel clip)
    {
        var startTime = clip.StartTimeMs;
        var duration = clip.DurationMs;
        var trackIndex = clip.TrackIndex;

        // 클립 제거
        _timeline.Clips.Remove(clip);

        // 같은 트랙의 이후 클립들 왼쪽으로 이동
        var affectedClips = _timeline.Clips
            .Where(c => c.TrackIndex == trackIndex && c.StartTimeMs > startTime)
            .ToList();

        foreach (var c in affectedClips)
        {
            c.StartTimeMs -= duration;
        }
    }

    /// <summary>
    /// 리플 이동: 클립 이동 시 같은 트랙의 이후 클립들도 함께 이동
    /// </summary>
    public void RippleMove(ClipModel clip, long newStartTime)
    {
        var oldStartTime = clip.StartTimeMs;
        var delta = newStartTime - oldStartTime;
        var trackIndex = clip.TrackIndex;

        // 클립 이동
        clip.StartTimeMs = newStartTime;

        // 같은 트랙의 이후 클립들도 함께 이동
        var affectedClips = _timeline.Clips
            .Where(c => c != clip &&
                       c.TrackIndex == trackIndex &&
                       c.StartTimeMs >= oldStartTime + clip.DurationMs)
            .ToList();

        foreach (var c in affectedClips)
        {
            c.StartTimeMs += delta;
        }
    }

    /// <summary>
    /// 리플 인서트: 특정 위치에 클립 삽입 시 이후 클립들을 오른쪽으로 밀어냄
    /// </summary>
    public void RippleInsert(ClipModel clip, long insertTime, int trackIndex)
    {
        clip.StartTimeMs = insertTime;
        clip.TrackIndex = trackIndex;

        // 삽입 위치 이후의 클립들을 오른쪽으로 이동
        var affectedClips = _timeline.Clips
            .Where(c => c.TrackIndex == trackIndex && c.StartTimeMs >= insertTime)
            .ToList();

        foreach (var c in affectedClips)
        {
            c.StartTimeMs += clip.DurationMs;
        }

        // 클립 추가
        _timeline.Clips.Add(clip);
    }

    /// <summary>
    /// 롤링 편집: 두 클립 경계를 이동 (앞 클립 늘어나면 뒤 클립 줄어듦, 전체 길이 유지)
    /// </summary>
    public void RollingEdit(ClipModel leftClip, ClipModel rightClip, long newBoundaryTime)
    {
        // 왼쪽 클립의 끝이 오른쪽 클립의 시작과 일치해야 함
        if (leftClip.EndTimeMs != rightClip.StartTimeMs)
        {
            throw new InvalidOperationException("Rolling edit requires adjacent clips");
        }

        var delta = newBoundaryTime - leftClip.EndTimeMs;

        // 왼쪽 클립 DurationMs 조정
        leftClip.DurationMs += delta;

        // 오른쪽 클립 StartTimeMs & DurationMs 조정
        rightClip.StartTimeMs += delta;
        rightClip.DurationMs -= delta;

        // 최소 길이 보장 (100ms)
        const long MinDuration = 100;
        if (leftClip.DurationMs < MinDuration || rightClip.DurationMs < MinDuration)
        {
            throw new InvalidOperationException("Clips must be at least 100ms long");
        }
    }
}
