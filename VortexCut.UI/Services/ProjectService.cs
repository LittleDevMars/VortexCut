using VortexCut.Core.Models;
using VortexCut.Interop.Services;

namespace VortexCut.UI.Services;

/// <summary>
/// 프로젝트 관리 서비스 (Rust Timeline/Renderer 연동)
/// </summary>
public class ProjectService : IDisposable
{
    private readonly TimelineService _timelineService;
    private readonly RenderService _renderService;
    private Project? _currentProject;
    private ulong _defaultVideoTrackId;
    private TimelineHandle? _timelineHandle;

    public Project? CurrentProject => _currentProject;

    public ProjectService()
    {
        _timelineService = new TimelineService();
        _renderService = new RenderService();
    }

    /// <summary>
    /// 새 프로젝트 생성
    /// </summary>
    public void CreateProject(string name, uint width = 1920, uint height = 1080, double fps = 30.0)
    {
        _currentProject = new Project(name, width, height, fps);
        _timelineService.CreateTimeline(width, height, fps);

        // 기본 비디오 트랙 생성
        _defaultVideoTrackId = _timelineService.AddVideoTrack();

        // Renderer 생성 (내부 TimelineHandle 사용 - Reflection으로 접근)
        var timelineField = typeof(TimelineService).GetField("_timeline",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _timelineHandle = (TimelineHandle?)timelineField?.GetValue(_timelineService);

        if (_timelineHandle != null)
        {
            _renderService.CreateRenderer(_timelineHandle);
        }
    }

    /// <summary>
    /// 비디오 클립 추가
    /// </summary>
    public ClipModel AddVideoClip(string filePath, long startTimeMs, long durationMs, int trackIndex = 0)
    {
        if (_currentProject == null)
            throw new InvalidOperationException("No project is open");

        var clipId = _timelineService.AddVideoClip(_defaultVideoTrackId, filePath, startTimeMs, durationMs);
        var clip = new ClipModel(clipId, filePath, startTimeMs, durationMs, trackIndex);
        _currentProject.Clips.Add(clip);

        return clip;
    }

    /// <summary>
    /// 특정 시간의 프레임 렌더링
    /// </summary>
    public RenderedFrame RenderFrame(long timestampMs)
    {
        return _renderService.RenderFrame(timestampMs);
    }

    public void Dispose()
    {
        _renderService.Dispose();
        _timelineService.Dispose();
    }
}
