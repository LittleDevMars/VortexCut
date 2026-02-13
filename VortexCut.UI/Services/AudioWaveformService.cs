using System.Runtime.InteropServices;
using VortexCut.Interop;

namespace VortexCut.UI.Services;

/// <summary>
/// 파형 데이터 (다운샘플링된 피크 배열)
/// </summary>
public class WaveformData
{
    /// <summary>피크 값 배열 (0.0~1.0 정규화, 모노 믹스다운)</summary>
    public float[] Peaks { get; set; } = Array.Empty<float>();

    /// <summary>다운샘플 비율 (원본 샘플 수 / 피크 1개)</summary>
    public uint SamplesPerPeak { get; set; }

    /// <summary>원본 샘플레이트</summary>
    public uint SampleRate { get; set; }

    /// <summary>채널 수</summary>
    public uint Channels { get; set; }

    /// <summary>오디오 총 길이 (ms)</summary>
    public long DurationMs { get; set; }

    /// <summary>생성 완료 여부</summary>
    public bool IsComplete { get; set; }

    /// <summary>생성 진행 중 여부</summary>
    public bool IsGenerating { get; set; }
}

/// <summary>
/// 오디오 파형 추출 + LRU 캐시 서비스
/// - Rust FFI (extract_audio_peaks)로 실제 오디오 디코딩
/// - 파일별 캐시 (최대 100개, 512MB)
/// - 백그라운드 비동기 생성
/// </summary>
public class AudioWaveformService : IDisposable
{
    // 캐시: 파일 경로 → 파형 데이터
    private readonly Dictionary<string, WaveformData> _cache = new();
    private readonly LinkedList<string> _lruOrder = new();
    private readonly object _lock = new();

    private const int MaxCacheEntries = 100;
    private const uint DefaultSamplesPerPeak = 512; // 44100Hz에서 약 86 피크/초

    // 동시 생성 제한 (FFmpeg 디코더)
    private readonly SemaphoreSlim _generationSemaphore = new(2);
    private CancellationTokenSource _cts = new();

    /// <summary>파형 준비 시 호출 (InvalidateVisual 트리거)</summary>
    public Action? OnWaveformReady { get; set; }

    /// <summary>
    /// 파형 데이터 조회/요청
    /// 캐시 히트: 즉시 반환
    /// 캐시 미스: 빈 WaveformData 반환 + 백그라운드 생성 시작
    /// </summary>
    public WaveformData? GetOrRequestWaveform(string filePath, long durationMs)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        lock (_lock)
        {
            // 캐시 히트
            if (_cache.TryGetValue(filePath, out var cached))
            {
                // LRU 갱신
                _lruOrder.Remove(filePath);
                _lruOrder.AddFirst(filePath);
                return cached;
            }

            // 캐시 미스: 빈 데이터 생성 + 백그라운드 요청
            var waveform = new WaveformData
            {
                SamplesPerPeak = DefaultSamplesPerPeak,
                DurationMs = durationMs,
                IsGenerating = true,
            };

            // LRU 관리
            EvictIfNeeded();
            _cache[filePath] = waveform;
            _lruOrder.AddFirst(filePath);
        }

        // 백그라운드 생성 시작
        var ct = _cts.Token;
        _ = Task.Run(() => GenerateWaveformAsync(filePath, ct), ct);

        return null; // 첫 요청은 null 반환 (아직 데이터 없음)
    }

    /// <summary>
    /// 백그라운드 파형 생성 (Rust FFI 호출)
    /// </summary>
    private async Task GenerateWaveformAsync(string filePath, CancellationToken ct)
    {
        try
        {
            await _generationSemaphore.WaitAsync(ct);

            if (ct.IsCancellationRequested) return;

            // Rust FFI 호출 (배경 스레드에서 실행)
            IntPtr filePathPtr = Marshal.StringToCoTaskMemUTF8(filePath);
            try
            {
                int result = NativeMethods.extract_audio_peaks(
                    filePathPtr,
                    DefaultSamplesPerPeak,
                    out IntPtr outPeaks,
                    out uint outPeakCount,
                    out uint outChannels,
                    out uint outSampleRate,
                    out long outDurationMs);

                if (result != 0 || outPeaks == IntPtr.Zero || outPeakCount == 0)
                {
                    // 추출 실패 → 캐시에서 제거
                    lock (_lock)
                    {
                        if (_cache.TryGetValue(filePath, out var w))
                        {
                            w.IsGenerating = false;
                        }
                    }
                    return;
                }

                // 피크 데이터 C# 배열로 복사
                var peaks = new float[outPeakCount];
                Marshal.Copy(outPeaks, peaks, 0, (int)outPeakCount);

                // Rust 메모리 해제
                NativeMethods.free_audio_peaks(outPeaks, outPeakCount);

                // 캐시 업데이트
                lock (_lock)
                {
                    if (_cache.TryGetValue(filePath, out var waveform))
                    {
                        waveform.Peaks = peaks;
                        waveform.Channels = outChannels;
                        waveform.SampleRate = outSampleRate;
                        waveform.DurationMs = outDurationMs;
                        waveform.IsComplete = true;
                        waveform.IsGenerating = false;
                    }
                }

                // UI 갱신 트리거
                OnWaveformReady?.Invoke();
            }
            finally
            {
                Marshal.FreeCoTaskMem(filePathPtr);
            }
        }
        catch (OperationCanceledException)
        {
            // 취소됨
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioWaveformService error: {ex.Message}");
        }
        finally
        {
            _generationSemaphore.Release();
        }
    }

    /// <summary>캐시 용량 초과 시 LRU 제거</summary>
    private void EvictIfNeeded()
    {
        while (_cache.Count >= MaxCacheEntries && _lruOrder.Count > 0)
        {
            var oldest = _lruOrder.Last!.Value;
            _lruOrder.RemoveLast();
            _cache.Remove(oldest);
        }
    }

    /// <summary>특정 파일 캐시 무효화</summary>
    public void InvalidateFile(string filePath)
    {
        lock (_lock)
        {
            _cache.Remove(filePath);
            _lruOrder.Remove(filePath);
        }
    }

    /// <summary>전체 캐시 클리어</summary>
    public void ClearAll()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        lock (_lock)
        {
            _cache.Clear();
            _lruOrder.Clear();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _generationSemaphore.Dispose();
    }
}
