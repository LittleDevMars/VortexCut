using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VortexCut.Interop.Types;

namespace VortexCut.Interop.Services;

/// <summary>
/// Timeline SafeHandle - 자동 메모리 관리
/// </summary>
public class TimelineHandle : SafeHandle
{
    public TimelineHandle(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        return NativeMethods.timeline_destroy(handle) == ErrorCodes.SUCCESS;
    }
}

/// <summary>
/// Timeline 관리 서비스
/// </summary>
public class TimelineService : IDisposable
{
    private TimelineHandle? _timeline;
    private bool _disposed;

    /// <summary>
    /// 타임라인 생성
    /// </summary>
    public void CreateTimeline(uint width, uint height, double fps)
    {
        ThrowIfDisposed();

        if (_timeline != null && !_timeline.IsInvalid)
        {
            throw new InvalidOperationException("Timeline already created");
        }

        int result = NativeMethods.timeline_create(width, height, fps, out IntPtr timelinePtr);
        CheckError(result);

        _timeline = new TimelineHandle(timelinePtr);
    }

    /// <summary>
    /// 비디오 트랙 추가
    /// </summary>
    public ulong AddVideoTrack()
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_add_video_track(_timeline!.DangerousGetHandle(), out ulong trackId);
        CheckError(result);

        return trackId;
    }

    /// <summary>
    /// 오디오 트랙 추가
    /// </summary>
    public ulong AddAudioTrack()
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_add_audio_track(_timeline!.DangerousGetHandle(), out ulong trackId);
        CheckError(result);

        return trackId;
    }

    /// <summary>
    /// 비디오 클립 추가
    /// </summary>
    public ulong AddVideoClip(ulong trackId, string filePath, long startTimeMs, long durationMs)
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        IntPtr filePathPtr = Marshal.StringToCoTaskMemUTF8(filePath);
        try
        {
            int result = NativeMethods.timeline_add_video_clip(
                _timeline!.DangerousGetHandle(),
                trackId,
                filePathPtr,
                startTimeMs,
                durationMs,
                out ulong clipId);

            CheckError(result);
            return clipId;
        }
        finally
        {
            Marshal.FreeCoTaskMem(filePathPtr);
        }
    }

    /// <summary>
    /// 오디오 클립 추가
    /// </summary>
    public ulong AddAudioClip(ulong trackId, string filePath, long startTimeMs, long durationMs)
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        IntPtr filePathPtr = Marshal.StringToCoTaskMemUTF8(filePath);
        try
        {
            int result = NativeMethods.timeline_add_audio_clip(
                _timeline!.DangerousGetHandle(),
                trackId,
                filePathPtr,
                startTimeMs,
                durationMs,
                out ulong clipId);

            CheckError(result);
            return clipId;
        }
        finally
        {
            Marshal.FreeCoTaskMem(filePathPtr);
        }
    }

    /// <summary>
    /// 비디오 클립 제거
    /// </summary>
    public void RemoveVideoClip(ulong trackId, ulong clipId)
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_remove_video_clip(_timeline!.DangerousGetHandle(), trackId, clipId);
        CheckError(result);
    }

    /// <summary>
    /// 오디오 클립 제거
    /// </summary>
    public void RemoveAudioClip(ulong trackId, ulong clipId)
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_remove_audio_clip(_timeline!.DangerousGetHandle(), trackId, clipId);
        CheckError(result);
    }

    /// <summary>
    /// 타임라인 총 길이 가져오기 (ms)
    /// </summary>
    public long GetDuration()
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_get_duration(_timeline!.DangerousGetHandle(), out long durationMs);
        CheckError(result);

        return durationMs;
    }

    /// <summary>
    /// 비디오 트랙 개수
    /// </summary>
    public int GetVideoTrackCount()
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_get_video_track_count(_timeline!.DangerousGetHandle(), out nuint count);
        CheckError(result);

        return (int)count;
    }

    /// <summary>
    /// 오디오 트랙 개수
    /// </summary>
    public int GetAudioTrackCount()
    {
        ThrowIfDisposed();
        ThrowIfNoTimeline();

        int result = NativeMethods.timeline_get_audio_track_count(_timeline!.DangerousGetHandle(), out nuint count);
        CheckError(result);

        return (int)count;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimelineService));
        }
    }

    private void ThrowIfNoTimeline()
    {
        if (_timeline == null || _timeline.IsInvalid)
        {
            throw new InvalidOperationException("Timeline not created");
        }
    }

    private static void CheckError(int errorCode)
    {
        if (errorCode != ErrorCodes.SUCCESS)
        {
            throw new RustException($"Rust error code: {errorCode}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timeline?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Rust FFI 에러 예외
/// </summary>
public class RustException : Exception
{
    public RustException(string message) : base(message) { }
}
