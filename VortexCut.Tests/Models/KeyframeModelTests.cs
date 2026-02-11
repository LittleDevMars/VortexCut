using VortexCut.Core.Models;
using Xunit;

namespace VortexCut.Tests.Models;

/// <summary>
/// KeyframeModel 단위 테스트 (After Effects 스타일 키프레임 시스템)
/// </summary>
public class KeyframeModelTests
{
    [Fact]
    public void Keyframe_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var keyframe = new Keyframe(1.5, 50.0, InterpolationType.EaseIn);

        // Assert
        Assert.Equal(1.5, keyframe.Time);
        Assert.Equal(50.0, keyframe.Value);
        Assert.Equal(InterpolationType.EaseIn, keyframe.Interpolation);
        Assert.Null(keyframe.InHandle);
        Assert.Null(keyframe.OutHandle);
    }

    [Fact]
    public void BezierHandle_Constructor_SetsOffsets()
    {
        // Arrange & Act
        var handle = new BezierHandle(0.5, 10.0);

        // Assert
        Assert.Equal(0.5, handle.TimeOffset);
        Assert.Equal(10.0, handle.ValueOffset);
    }

    [Fact]
    public void AddKeyframe_AddsAndSortsKeyframes()
    {
        // Arrange
        var system = new KeyframeSystem();

        // Act
        system.AddKeyframe(2.0, 100.0);
        system.AddKeyframe(0.5, 50.0);
        system.AddKeyframe(1.0, 75.0);

        // Assert
        Assert.Equal(3, system.Keyframes.Count);
        Assert.Equal(0.5, system.Keyframes[0].Time);
        Assert.Equal(1.0, system.Keyframes[1].Time);
        Assert.Equal(2.0, system.Keyframes[2].Time);
    }

    [Fact]
    public void AddKeyframe_FiresOnKeyframeAddedEvent()
    {
        // Arrange
        var system = new KeyframeSystem();
        Keyframe? addedKeyframe = null;
        system.OnKeyframeAdded += (kf) => addedKeyframe = kf;

        // Act
        system.AddKeyframe(1.0, 50.0);

        // Assert
        Assert.NotNull(addedKeyframe);
        Assert.Equal(1.0, addedKeyframe.Time);
        Assert.Equal(50.0, addedKeyframe.Value);
    }

    [Fact]
    public void RemoveKeyframe_RemovesKeyframeAndFiresEvent()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 50.0);
        var keyframeToRemove = system.Keyframes[0];
        Keyframe? removedKeyframe = null;
        system.OnKeyframeRemoved += (kf) => removedKeyframe = kf;

        // Act
        system.RemoveKeyframe(keyframeToRemove);

        // Assert
        Assert.Empty(system.Keyframes);
        Assert.NotNull(removedKeyframe);
        Assert.Equal(keyframeToRemove, removedKeyframe);
    }

    [Fact]
    public void UpdateKeyframe_UpdatesTimeAndValueAndResorts()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0);
        system.AddKeyframe(1.0, 50.0);
        system.AddKeyframe(2.0, 100.0);
        var middleKeyframe = system.Keyframes[1];

        // Act - 중간 키프레임을 마지막으로 이동
        system.UpdateKeyframe(middleKeyframe, 3.0, 150.0);

        // Assert
        Assert.Equal(3.0, middleKeyframe.Time);
        Assert.Equal(150.0, middleKeyframe.Value);
        Assert.Equal(3.0, system.Keyframes[2].Time); // 마지막 위치로 재정렬
    }

    [Fact]
    public void ClearKeyframes_RemovesAllAndFiresEvents()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0);
        system.AddKeyframe(1.0, 50.0);
        system.AddKeyframe(2.0, 100.0);
        int removedCount = 0;
        system.OnKeyframeRemoved += (_) => removedCount++;

        // Act
        system.ClearKeyframes();

        // Assert
        Assert.Empty(system.Keyframes);
        Assert.Equal(3, removedCount);
    }

    [Fact]
    public void Interpolate_NoKeyframes_ReturnsZero()
    {
        // Arrange
        var system = new KeyframeSystem();

        // Act
        var result = system.Interpolate(1.0);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Interpolate_SingleKeyframe_ReturnsValue()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 50.0);

        // Act & Assert
        Assert.Equal(50.0, system.Interpolate(0.0)); // 이전
        Assert.Equal(50.0, system.Interpolate(1.0)); // 정확히
        Assert.Equal(50.0, system.Interpolate(2.0)); // 이후
    }

    [Fact]
    public void Interpolate_BeforeFirstKeyframe_ReturnsFirstValue()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 50.0);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var result = system.Interpolate(0.5);

        // Assert
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void Interpolate_AfterLastKeyframe_ReturnsLastValue()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 50.0);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var result = system.Interpolate(3.0);

        // Assert
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void Interpolate_Linear_CorrectMidpoint()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.Linear);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var result = system.Interpolate(1.0); // 중간 지점

        // Assert
        Assert.Equal(50.0, result, precision: 2);
    }

    [Fact]
    public void Interpolate_EaseIn_AcceleratesCurve()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.EaseIn);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var quarter = system.Interpolate(0.5);  // 25% 시간
        var midpoint = system.Interpolate(1.0); // 50% 시간

        // Assert - Ease In은 처음에 느리므로 선형보다 작아야 함
        Assert.True(quarter < 25.0, $"Quarter point {quarter} should be < 25.0 (linear)");
        Assert.True(midpoint < 50.0, $"Midpoint {midpoint} should be < 50.0 (linear)");
    }

    [Fact]
    public void Interpolate_EaseOut_DeceleratesCurve()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.EaseOut);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var quarter = system.Interpolate(0.5);  // 25% 시간
        var midpoint = system.Interpolate(1.0); // 50% 시간

        // Assert - Ease Out은 처음에 빠르므로 선형보다 커야 함
        Assert.True(quarter > 25.0, $"Quarter point {quarter} should be > 25.0 (linear)");
        Assert.True(midpoint > 50.0, $"Midpoint {midpoint} should be > 50.0 (linear)");
    }

    [Fact]
    public void Interpolate_EaseInOut_SCurve()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.EaseInOut);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var quarter = system.Interpolate(0.5);   // 25% 시간
        var midpoint = system.Interpolate(1.0);  // 50% 시간 (정확히 50이어야 함)
        var threeQuarter = system.Interpolate(1.5); // 75% 시간

        // Assert
        Assert.True(quarter < 25.0, "EaseInOut should be < 25 at 25% time");
        Assert.Equal(50.0, midpoint, precision: 2); // S-curve의 중심점
        Assert.True(threeQuarter > 75.0, "EaseInOut should be > 75 at 75% time");
    }

    [Fact]
    public void Interpolate_Hold_StairStep()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.Hold);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var justBefore = system.Interpolate(1.99);
        var atKeyframe = system.Interpolate(2.0);

        // Assert - Hold는 다음 키프레임까지 값 유지
        Assert.Equal(0.0, justBefore);
        Assert.Equal(100.0, atKeyframe);
    }

    [Fact]
    public void Interpolate_Bezier_WithoutHandles_UsesDefaultCurve()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.Bezier);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var midpoint = system.Interpolate(1.0);

        // Assert - 핸들이 없으면 1/3, 2/3 지점 사용 (부드러운 곡선)
        Assert.InRange(midpoint, 40.0, 60.0); // 대략 중간값
    }

    [Fact]
    public void Interpolate_Bezier_WithHandles_UsesCustomCurve()
    {
        // Arrange
        var system = new KeyframeSystem();
        var kf1 = new Keyframe(0.0, 0.0, InterpolationType.Bezier)
        {
            OutHandle = new BezierHandle(0.5, 80.0) // 큰 값 오프셋
        };
        var kf2 = new Keyframe(2.0, 100.0)
        {
            InHandle = new BezierHandle(-0.5, -20.0)
        };

        system.Keyframes.Add(kf1);
        system.Keyframes.Add(kf2);

        // Act
        var midpoint = system.Interpolate(1.0);

        // Assert - 핸들이 값을 위로 당기므로 50.0보다 커야 함
        Assert.True(midpoint > 50.0, $"Bezier with upward handles: {midpoint} should be > 50.0");
    }

    [Fact]
    public void Interpolate_MultipleSegments_CorrectBehavior()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, InterpolationType.Linear);
        system.AddKeyframe(1.0, 50.0, InterpolationType.EaseIn);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var firstSegment = system.Interpolate(0.5);  // 0~1 (Linear)
        var secondSegment = system.Interpolate(1.5); // 1~2 (EaseIn)

        // Assert
        Assert.Equal(25.0, firstSegment, precision: 2); // Linear midpoint
        Assert.True(secondSegment < 75.0, "EaseIn segment should be < 75.0");
    }

    [Theory]
    [InlineData(InterpolationType.Linear, 1.0, 50.0)]
    [InlineData(InterpolationType.Hold, 1.0, 0.0)]
    [InlineData(InterpolationType.Hold, 2.0, 100.0)]
    public void Interpolate_VariousTypes_CorrectValues(InterpolationType type, double time, double expectedValue)
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0.0, type);
        system.AddKeyframe(2.0, 100.0);

        // Act
        var result = system.Interpolate(time);

        // Assert
        Assert.Equal(expectedValue, result, precision: 2);
    }
}
