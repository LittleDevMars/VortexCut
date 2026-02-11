using System.Runtime.InteropServices;
using VortexCut.Interop;
using VortexCut.Tests.Helpers;
using Xunit;

namespace VortexCut.Tests.Interop;

/// <summary>
/// Rust FFI 기본 테스트 (네이티브 DLL 필요)
/// </summary>
public class NativeMethodsTests
{
    [FactRequiresNativeDll]
    public void HelloWorld_ReturnsMessage()
    {
        // Arrange & Act
        IntPtr messagePtr = NativeMethods.hello_world();

        // Assert
        Assert.NotEqual(IntPtr.Zero, messagePtr);

        string? message = Marshal.PtrToStringUTF8(messagePtr);
        Assert.NotNull(message);
        Assert.Equal("Hello from Rust!", message);

        // Cleanup
        NativeMethods.string_free(messagePtr);
    }

    [FactRequiresNativeDll]
    public void AddNumbers_ReturnsCorrectSum()
    {
        // Arrange
        int a = 10;
        int b = 20;

        // Act
        int result = NativeMethods.add_numbers(a, b);

        // Assert
        Assert.Equal(30, result);
    }

    [TheoryRequiresNativeDll]
    [InlineData(5, 3, 8)]
    [InlineData(-5, 3, -2)]
    [InlineData(0, 0, 0)]
    [InlineData(1000000, 2000000, 3000000)]
    public void AddNumbers_VariousInputs_ReturnsCorrectSum(int a, int b, int expected)
    {
        // Act
        int result = NativeMethods.add_numbers(a, b);

        // Assert
        Assert.Equal(expected, result);
    }
}
