using VortexCut.Interop.Services;
using VortexCut.Tests.Helpers;
using Xunit;

namespace VortexCut.Tests.Services;

/// <summary>
/// TimelineService 통합 테스트 (네이티브 DLL 필요)
/// </summary>
public class TimelineServiceTests
{
    [FactRequiresNativeDll]
    public void CreateTimeline_ValidParameters_Success()
    {
        // Arrange & Act
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);

        // Assert
        Assert.Equal(0, service.GetVideoTrackCount());
        Assert.Equal(0, service.GetAudioTrackCount());
        Assert.Equal(0, service.GetDuration());
    }

    [FactRequiresNativeDll]
    public void AddVideoTrack_Success()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);

        // Act
        ulong trackId = service.AddVideoTrack();

        // Assert
        Assert.NotEqual(0UL, trackId);
        Assert.Equal(1, service.GetVideoTrackCount());
    }

    [FactRequiresNativeDll]
    public void AddAudioTrack_Success()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);

        // Act
        ulong trackId = service.AddAudioTrack();

        // Assert
        Assert.NotEqual(0UL, trackId);
        Assert.Equal(1, service.GetAudioTrackCount());
    }

    [FactRequiresNativeDll]
    public void AddVideoClip_ValidParameters_Success()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);
        ulong trackId = service.AddVideoTrack();

        // Act
        ulong clipId = service.AddVideoClip(trackId, "test.mp4", 0, 5000);

        // Assert
        Assert.NotEqual(0UL, clipId);
        Assert.Equal(5000, service.GetDuration());
    }

    [FactRequiresNativeDll]
    public void AddMultipleClips_CalculatesDurationCorrectly()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);
        ulong trackId = service.AddVideoTrack();

        // Act
        service.AddVideoClip(trackId, "clip1.mp4", 0, 5000);
        service.AddVideoClip(trackId, "clip2.mp4", 5000, 3000);

        // Assert
        Assert.Equal(8000, service.GetDuration());
    }

    [FactRequiresNativeDll]
    public void RemoveVideoClip_Success()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);
        ulong trackId = service.AddVideoTrack();
        ulong clipId = service.AddVideoClip(trackId, "test.mp4", 0, 5000);

        // Act
        service.RemoveVideoClip(trackId, clipId);

        // Assert
        Assert.Equal(0, service.GetDuration());
    }

    [FactRequiresNativeDll]
    public void AddAudioClip_ValidParameters_Success()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);
        ulong trackId = service.AddAudioTrack();

        // Act
        ulong clipId = service.AddAudioClip(trackId, "audio.mp3", 0, 10000);

        // Assert
        Assert.NotEqual(0UL, clipId);
        Assert.Equal(10000, service.GetDuration());
    }

    [FactRequiresNativeDll]
    public void MultipleTracksAndClips_ComplexScenario()
    {
        // Arrange
        using var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);

        // Act
        ulong videoTrack1 = service.AddVideoTrack();
        ulong videoTrack2 = service.AddVideoTrack();
        ulong audioTrack = service.AddAudioTrack();

        service.AddVideoClip(videoTrack1, "v1.mp4", 0, 5000);
        service.AddVideoClip(videoTrack2, "v2.mp4", 2000, 4000);
        service.AddAudioClip(audioTrack, "a1.mp3", 0, 8000);

        // Assert
        Assert.Equal(2, service.GetVideoTrackCount());
        Assert.Equal(1, service.GetAudioTrackCount());
        Assert.Equal(8000, service.GetDuration()); // 오디오가 가장 길음
    }

    [FactRequiresNativeDll]
    public void Dispose_MultipleTimes_NoError()
    {
        // Arrange
        var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);

        // Act & Assert - 여러 번 Dispose 호출해도 에러 없음
        service.Dispose();
        service.Dispose();
        service.Dispose();
    }

    [FactRequiresNativeDll]
    public void OperationAfterDispose_ThrowsException()
    {
        // Arrange
        var service = new TimelineService();
        service.CreateTimeline(1920, 1080, 30.0);
        service.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => service.AddVideoTrack());
    }

    [FactRequiresNativeDll]
    public void OperationWithoutTimeline_ThrowsException()
    {
        // Arrange
        using var service = new TimelineService();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.AddVideoTrack());
    }
}
