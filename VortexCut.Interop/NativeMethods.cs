using System.Runtime.InteropServices;

namespace VortexCut.Interop;

/// <summary>
/// Rust 네이티브 라이브러리 P/Invoke 선언
/// </summary>
public static class NativeMethods
{
    private const string DllName = "rust_engine";

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
}
