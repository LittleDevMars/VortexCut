using VortexCut.Core.Models;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Services;

/// <summary>
/// 링크 클립 서비스 (비디오+오디오 자동 연결)
/// </summary>
public class LinkClipService
{
    private readonly TimelineViewModel _timeline;

    public LinkClipService(TimelineViewModel timeline)
    {
        _timeline = timeline;
    }

    /// <summary>
    /// 두 클립을 링크 (비디오 ↔ 오디오)
    /// </summary>
    public void LinkClips(ClipModel videoClip, ClipModel audioClip)
    {
        if (videoClip == null || audioClip == null)
            throw new ArgumentNullException("Clips cannot be null");

        // 기존 링크 해제
        if (videoClip.IsLinked)
            UnlinkClip(videoClip);
        if (audioClip.IsLinked)
            UnlinkClip(audioClip);

        // 새 링크 설정
        videoClip.LinkedAudioClipId = audioClip.Id;
        audioClip.LinkedVideoClipId = videoClip.Id;
    }

    /// <summary>
    /// 클립 링크 해제
    /// </summary>
    public void UnlinkClip(ClipModel clip)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        // 오디오 클립 링크 해제
        if (clip.LinkedAudioClipId.HasValue)
        {
            var linkedClip = FindClip(clip.LinkedAudioClipId.Value);
            if (linkedClip != null)
            {
                linkedClip.LinkedVideoClipId = null;
            }
            clip.LinkedAudioClipId = null;
        }

        // 비디오 클립 링크 해제
        if (clip.LinkedVideoClipId.HasValue)
        {
            var linkedClip = FindClip(clip.LinkedVideoClipId.Value);
            if (linkedClip != null)
            {
                linkedClip.LinkedAudioClipId = null;
            }
            clip.LinkedVideoClipId = null;
        }
    }

    /// <summary>
    /// 링크된 클립과 함께 이동
    /// </summary>
    public void MoveLinkedClips(ClipModel clip, long deltaTimeMs)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        clip.StartTimeMs += deltaTimeMs;

        // 링크된 오디오 클립도 함께 이동
        if (clip.LinkedAudioClipId.HasValue)
        {
            var linkedClip = FindClip(clip.LinkedAudioClipId.Value);
            if (linkedClip != null)
            {
                linkedClip.StartTimeMs += deltaTimeMs;
            }
        }

        // 링크된 비디오 클립도 함께 이동
        if (clip.LinkedVideoClipId.HasValue)
        {
            var linkedClip = FindClip(clip.LinkedVideoClipId.Value);
            if (linkedClip != null)
            {
                linkedClip.StartTimeMs += deltaTimeMs;
            }
        }
    }

    /// <summary>
    /// 링크된 클립과 함께 삭제
    /// </summary>
    public void DeleteLinkedClips(ClipModel clip)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        // 링크된 클립 찾기
        ClipModel? linkedClip = null;

        if (clip.LinkedAudioClipId.HasValue)
        {
            linkedClip = FindClip(clip.LinkedAudioClipId.Value);
        }
        else if (clip.LinkedVideoClipId.HasValue)
        {
            linkedClip = FindClip(clip.LinkedVideoClipId.Value);
        }

        // 두 클립 모두 삭제
        _timeline.Clips.Remove(clip);
        if (linkedClip != null)
        {
            _timeline.Clips.Remove(linkedClip);
        }
    }

    /// <summary>
    /// 링크된 클립 가져오기
    /// </summary>
    public ClipModel? GetLinkedClip(ClipModel clip)
    {
        if (clip == null)
            return null;

        if (clip.LinkedAudioClipId.HasValue)
        {
            return FindClip(clip.LinkedAudioClipId.Value);
        }

        if (clip.LinkedVideoClipId.HasValue)
        {
            return FindClip(clip.LinkedVideoClipId.Value);
        }

        return null;
    }

    /// <summary>
    /// 자동 링크: 같은 파일에서 나온 비디오/오디오 클립 자동 연결
    /// </summary>
    public void AutoLinkClipsFromSameFile(string filePath, long startTimeMs)
    {
        // 같은 파일에서 같은 시작 시간의 클립들 찾기
        var clipsFromFile = _timeline.Clips
            .Where(c => c.FilePath == filePath && c.StartTimeMs == startTimeMs)
            .ToList();

        if (clipsFromFile.Count >= 2)
        {
            // 첫 번째는 비디오, 두 번째는 오디오로 가정
            var videoClip = clipsFromFile[0];
            var audioClip = clipsFromFile[1];
            LinkClips(videoClip, audioClip);
        }
    }

    /// <summary>
    /// 클립 ID로 찾기
    /// </summary>
    private ClipModel? FindClip(ulong clipId)
    {
        return _timeline.Clips.FirstOrDefault(c => c.Id == clipId);
    }
}
