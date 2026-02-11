using System.Runtime.InteropServices;
using Xunit;

namespace VortexCut.Tests.Helpers;

/// <summary>
/// 네이티브 DLL이 있을 때만 실행되는 Fact 속성
/// CI/CD 환경에서 DLL이 없으면 테스트를 스킵
/// </summary>
public sealed class FactRequiresNativeDllAttribute : FactAttribute
{
    private const string RUST_DLL_NAME = "rust_engine";

    public FactRequiresNativeDllAttribute()
    {
        if (!IsNativeDllAvailable())
        {
            Skip = $"Skipped: Native DLL '{RUST_DLL_NAME}' not found. This test requires Rust engine.";
        }
    }

    private static bool IsNativeDllAvailable()
    {
        try
        {
            // DLL 로드 시도 (실제 함수 호출은 하지 않음)
            NativeLibrary.TryLoad(RUST_DLL_NAME, out IntPtr handle);
            if (handle != IntPtr.Zero)
            {
                NativeLibrary.Free(handle);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 네이티브 DLL이 있을 때만 실행되는 Theory 속성
/// </summary>
public sealed class TheoryRequiresNativeDllAttribute : TheoryAttribute
{
    private const string RUST_DLL_NAME = "rust_engine";

    public TheoryRequiresNativeDllAttribute()
    {
        if (!IsNativeDllAvailable())
        {
            Skip = $"Skipped: Native DLL '{RUST_DLL_NAME}' not found. This test requires Rust engine.";
        }
    }

    private static bool IsNativeDllAvailable()
    {
        try
        {
            NativeLibrary.TryLoad(RUST_DLL_NAME, out IntPtr handle);
            if (handle != IntPtr.Zero)
            {
                NativeLibrary.Free(handle);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
