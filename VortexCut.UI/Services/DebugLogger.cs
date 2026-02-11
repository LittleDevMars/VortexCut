using System;
using System.Diagnostics;
using System.IO;

namespace VortexCut.UI.Services;

/// <summary>
/// 디버그 로그를 파일과 Output 창에 동시에 기록
/// </summary>
public static class DebugLogger
{
    private static readonly string LogFilePath = Path.Combine(
        Path.GetTempPath(),
        "vortexcut_debug.log"
    );

    private static readonly object LockObj = new();
    private static bool _isInitialized = false;

    /// <summary>
    /// 로거 초기화 (애플리케이션 시작 시 호출)
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            // 기존 로그 파일 삭제 (새 세션 시작)
            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }

            Log($"=== VortexCut Debug Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            Log($"Log file: {LogFilePath}");

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize logger: {ex.Message}");
        }
    }

    /// <summary>
    /// 로그 메시지 기록 (파일 + Output 창)
    /// </summary>
    public static void Log(string message)
    {
        var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        // Output 창에 출력
        Debug.WriteLine(timestampedMessage);

        // 파일에 기록 (thread-safe)
        try
        {
            lock (LockObj)
            {
                File.AppendAllText(LogFilePath, timestampedMessage + Environment.NewLine);
            }
        }
        catch
        {
            // 파일 쓰기 실패 시 무시 (Output 창에는 이미 출력됨)
        }
    }

    /// <summary>
    /// 로그 파일 경로 가져오기
    /// </summary>
    public static string GetLogFilePath() => LogFilePath;
}
