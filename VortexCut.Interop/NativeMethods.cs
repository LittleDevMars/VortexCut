using System.Runtime.InteropServices;

namespace VortexCut.Interop;

/// <summary>
/// Rust 네이티브 라이브러리 P/Invoke 선언
/// </summary>
public static class NativeMethods
{
    // 크로스 플랫폼 라이브러리 이름
    // Windows: rust_engine.dll
    // macOS/Linux: librust_engine.dylib/so (lib 접두사 필요)
#if WINDOWS
    private const string DllName = "rust_engine";
#else
    private const string DllName = "librust_engine";
#endif

    /// <summary>
    /// Rust에서 할당한 문자열 메모리 해제
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void string_free(IntPtr ptr);

    /// <summary>
    /// Hello World 테스트 함수
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr hello_world();

    /// <summary>
    /// 두 수를 더하는 테스트 함수
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int add_numbers(int a, int b);

    // ==================== Timeline Functions ====================

    /// <summary>
    /// Timeline 생성
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_create(uint width, uint height, double fps, out IntPtr outTimeline);

    /// <summary>
    /// Timeline 파괴
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_destroy(IntPtr timeline);

    /// <summary>
    /// 비디오 트랙 추가
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_add_video_track(IntPtr timeline, out ulong outTrackId);

    /// <summary>
    /// 오디오 트랙 추가
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_add_audio_track(IntPtr timeline, out ulong outTrackId);

    /// <summary>
    /// 비디오 클립 추가
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_add_video_clip(
        IntPtr timeline,
        ulong trackId,
        IntPtr filePath,
        long startTimeMs,
        long durationMs,
        out ulong outClipId);

    /// <summary>
    /// 오디오 클립 추가
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_add_audio_clip(
        IntPtr timeline,
        ulong trackId,
        IntPtr filePath,
        long startTimeMs,
        long durationMs,
        out ulong outClipId);

    /// <summary>
    /// 비디오 클립 제거
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_remove_video_clip(IntPtr timeline, ulong trackId, ulong clipId);

    /// <summary>
    /// 오디오 클립 제거
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_remove_audio_clip(IntPtr timeline, ulong trackId, ulong clipId);

    /// <summary>
    /// 타임라인 총 길이 가져오기
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_get_duration(IntPtr timeline, out long outDurationMs);

    /// <summary>
    /// 비디오 트랙 개수 가져오기
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_get_video_track_count(IntPtr timeline, out nuint outCount);

    /// <summary>
    /// 오디오 트랙 개수 가져오기
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_get_audio_track_count(IntPtr timeline, out nuint outCount);

    /// <summary>
    /// 특정 비디오 트랙의 클립 개수 가져오기
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int timeline_get_video_clip_count(IntPtr timeline, ulong trackId, out nuint outCount);

    // ==================== Video Info Functions ====================

    /// <summary>
    /// 비디오 파일 정보 조회 (duration, width, height, fps)
    /// filePath는 UTF-8 인코딩된 IntPtr (Marshal.StringToCoTaskMemUTF8 사용)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_video_info(
        IntPtr filePath,
        out long outDurationMs,
        out uint outWidth,
        out uint outHeight,
        out double outFps);

    // ==================== Renderer Functions ====================

    /// <summary>
    /// Renderer 생성
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_create(IntPtr timeline, out IntPtr outRenderer);

    /// <summary>
    /// Renderer 파괴
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_destroy(IntPtr renderer);

    /// <summary>
    /// 프레임 렌더링
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_render_frame(
        IntPtr renderer,
        long timestampMs,
        out uint outWidth,
        out uint outHeight,
        out IntPtr outData,
        out nuint outDataSize);

    /// <summary>
    /// 재생 모드 설정 (재생 시작 시 1, 정지 시 0)
    /// 재생 모드: forward_threshold=5000ms (seek 대신 forward decode)
    /// 스크럽 모드: forward_threshold=100ms (즉시 seek)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_set_playback_mode(IntPtr renderer, int playback);

    /// <summary>
    /// 프레임 캐시 클리어 (클립 편집 시 호출)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_clear_cache(IntPtr renderer);

    /// <summary>
    /// 캐시 통계 조회 (디버깅/모니터링)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_get_cache_stats(
        IntPtr renderer,
        out uint outCachedFrames,
        out nuint outCacheBytes);

    /// <summary>
    /// 렌더링된 프레임 데이터 해제
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int renderer_free_frame_data(IntPtr data, nuint size);

    /// <summary>
    /// 비디오 썸네일 생성 (스탠드얼론 함수)
    /// filePath는 UTF-8 인코딩된 IntPtr (Marshal.StringToCoTaskMemUTF8 사용)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int generate_video_thumbnail(
        IntPtr filePath,
        long timestampMs,
        uint thumbWidth,
        uint thumbHeight,
        out uint outWidth,
        out uint outHeight,
        out IntPtr outData,
        out nuint outDataSize);

    // ==================== Thumbnail Session Functions ====================

    /// <summary>
    /// 썸네일 세션 생성 (디코더를 한 번 열고 여러 프레임 생성)
    /// filePath는 UTF-8 인코딩된 IntPtr
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int thumbnail_session_create(
        IntPtr filePath,
        uint thumbWidth,
        uint thumbHeight,
        out IntPtr outSession,
        out long outDurationMs,
        out double outFps);

    /// <summary>
    /// 세션에서 특정 시간의 썸네일 생성
    /// width=0, height=0이면 프레임 스킵 (FrameSkipped/EOF)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int thumbnail_session_generate(
        IntPtr session,
        long timestampMs,
        out uint outWidth,
        out uint outHeight,
        out IntPtr outData,
        out nuint outDataSize);

    /// <summary>
    /// 썸네일 세션 파괴
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int thumbnail_session_destroy(IntPtr session);

    // ==================== Audio Waveform Functions ====================

    /// <summary>
    /// 오디오 파형 피크 데이터 추출
    /// filePath는 UTF-8 인코딩된 IntPtr (Marshal.StringToCoTaskMemUTF8 사용)
    /// samples_per_peak: 다운샘플 비율 (예: 1024 → 1024 샘플당 1 피크)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int extract_audio_peaks(
        IntPtr filePath,
        uint samplesPerPeak,
        out IntPtr outPeaks,
        out uint outPeakCount,
        out uint outChannels,
        out uint outSampleRate,
        out long outDurationMs);

    /// <summary>
    /// 오디오 피크 데이터 메모리 해제
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int free_audio_peaks(IntPtr peaks, uint count);
}
