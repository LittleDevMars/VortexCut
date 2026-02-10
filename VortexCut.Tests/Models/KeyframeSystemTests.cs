using VortexCut.Core.Models;
using Xunit;

namespace VortexCut.Tests.Models;

/// <summary>
/// KeyframeSystem 단위 테스트
/// </summary>
public class KeyframeSystemTests
{
    [Fact]
    public void AddKeyframe_AddsAndSorts()
    {
        // Arrange
        var system = new KeyframeSystem();

        // Act
        system.AddKeyframe(2.0, 100);
        system.AddKeyframe(0.0, 50);
        system.AddKeyframe(1.0, 75);

        // Assert
        Assert.Equal(3, system.Keyframes.Count);
        Assert.Equal(0.0, system.Keyframes[0].Time);
        Assert.Equal(1.0, system.Keyframes[1].Time);
        Assert.Equal(2.0, system.Keyframes[2].Time);
    }

    [Fact]
    public void RemoveKeyframe_RemovesKeyframe()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 50);
        system.AddKeyframe(1.0, 75);
        var toRemove = system.Keyframes[0];

        // Act
        system.RemoveKeyframe(toRemove);

        // Assert
        Assert.Single(system.Keyframes);
        Assert.Equal(1.0, system.Keyframes[0].Time);
    }

    [Fact]
    public void ClearKeyframes_RemovesAll()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 50);
        system.AddKeyframe(1.0, 75);
        system.AddKeyframe(2.0, 100);

        // Act
        system.ClearKeyframes();

        // Assert
        Assert.Empty(system.Keyframes);
    }

    [Fact]
    public void Interpolate_NoKeyframes_ReturnsZero()
    {
        // Arrange
        var system = new KeyframeSystem();

        // Act
        var value = system.Interpolate(1.0);

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void Interpolate_SingleKeyframe_ReturnsKeyframeValue()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 100);

        // Act
        var value = system.Interpolate(2.0);

        // Assert
        Assert.Equal(100, value);
    }

    [Fact]
    public void Interpolate_Linear_CalculatesCorrectly()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0, InterpolationType.Linear);
        system.AddKeyframe(1.0, 100, InterpolationType.Linear);

        // Act
        var value = system.Interpolate(0.5);

        // Assert
        Assert.Equal(50, value);
    }

    [Fact]
    public void Interpolate_EaseIn_Accelerates()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0, InterpolationType.EaseIn);
        system.AddKeyframe(1.0, 100, InterpolationType.EaseIn);

        // Act
        var value25 = system.Interpolate(0.25); // t=0.25 → t²=0.0625
        var value50 = system.Interpolate(0.5);  // t=0.5 → t²=0.25
        var value75 = system.Interpolate(0.75); // t=0.75 → t²=0.5625

        // Assert: 가속 곡선 (초반 느림, 후반 빠름)
        Assert.True(value25 < 25);   // 6.25
        Assert.True(value50 < 50);   // 25
        Assert.True(value75 > 50);   // 56.25
    }

    [Fact]
    public void Interpolate_EaseOut_Decelerates()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0, InterpolationType.EaseOut);
        system.AddKeyframe(1.0, 100, InterpolationType.EaseOut);

        // Act
        var value25 = system.Interpolate(0.25);
        var value50 = system.Interpolate(0.5);
        var value75 = system.Interpolate(0.75);

        // Assert: 감속 곡선 (초반 빠름, 후반 느림)
        Assert.True(value25 > 0);    // 43.75
        Assert.True(value50 > 50);   // 75
        Assert.True(value75 > 75);   // 93.75
    }

    [Fact]
    public void Interpolate_EaseInOut_SmoothCurve()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0, InterpolationType.EaseInOut);
        system.AddKeyframe(1.0, 100, InterpolationType.EaseInOut);

        // Act
        var value25 = system.Interpolate(0.25);
        var value50 = system.Interpolate(0.5);
        var value75 = system.Interpolate(0.75);

        // Assert: S자 곡선 (초반 가속, 중반 일정, 후반 감속)
        Assert.True(value25 < 25);
        Assert.Equal(50, value50, 1); // 중간값
        Assert.True(value75 > 75);
    }

    [Fact]
    public void Interpolate_Hold_ReturnsFirstKeyframeValue()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 50, InterpolationType.Hold);
        system.AddKeyframe(1.0, 100, InterpolationType.Hold);

        // Act
        var value25 = system.Interpolate(0.25);
        var value75 = system.Interpolate(0.75);
        var value99 = system.Interpolate(0.99);

        // Assert: 계단식 (다음 키프레임 직전까지 이전 값 유지)
        Assert.Equal(50, value25);
        Assert.Equal(50, value75);
        Assert.Equal(50, value99);
    }

    [Fact]
    public void Interpolate_Bezier_CalculatesCubicCurve()
    {
        // Arrange
        var system = new KeyframeSystem();

        var kf1 = new Keyframe(0.0, 0, InterpolationType.Bezier);
        kf1.OutHandle = new BezierHandle(0.1, 100); // 핸들이 크게 위로 올라감

        var kf2 = new Keyframe(1.0, 100, InterpolationType.Bezier);
        kf2.InHandle = new BezierHandle(-0.1, 0); // 핸들이 중립

        system.Keyframes.Add(kf1);
        system.Keyframes.Add(kf2);

        // Act
        var value25 = system.Interpolate(0.25);
        var value50 = system.Interpolate(0.5);
        var value75 = system.Interpolate(0.75);

        // Assert: 베지어 곡선 (0과 100 사이의 값)
        Assert.True(value25 >= 0 && value25 <= 100);
        Assert.True(value50 >= 0 && value50 <= 100);
        Assert.True(value75 >= 0 && value75 <= 100);
    }

    [Fact]
    public void Interpolate_OutOfRange_ReturnsEdgeValues()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 50);
        system.AddKeyframe(2.0, 100);

        // Act
        var valueBefore = system.Interpolate(0.5); // 첫 키프레임 전
        var valueAfter = system.Interpolate(3.0);  // 마지막 키프레임 후

        // Assert
        Assert.Equal(50, valueBefore);
        Assert.Equal(100, valueAfter);
    }

    [Fact]
    public void UpdateKeyframe_UpdatesAndResorts()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(0.0, 0);
        system.AddKeyframe(1.0, 50);
        system.AddKeyframe(2.0, 100);

        var middleKeyframe = system.Keyframes[1];

        // Act: 중간 키프레임을 3초로 이동
        system.UpdateKeyframe(middleKeyframe, 3.0, 150);

        // Assert
        Assert.Equal(3, system.Keyframes.Count);
        Assert.Equal(0.0, system.Keyframes[0].Time);
        Assert.Equal(2.0, system.Keyframes[1].Time);
        Assert.Equal(3.0, system.Keyframes[2].Time); // 재정렬됨
        Assert.Equal(150, system.Keyframes[2].Value);
    }

    [Fact]
    public void OnKeyframeAdded_Event_Fires()
    {
        // Arrange
        var system = new KeyframeSystem();
        Keyframe? addedKeyframe = null;
        system.OnKeyframeAdded += (kf) => addedKeyframe = kf;

        // Act
        system.AddKeyframe(1.0, 50);

        // Assert
        Assert.NotNull(addedKeyframe);
        Assert.Equal(1.0, addedKeyframe.Time);
        Assert.Equal(50, addedKeyframe.Value);
    }

    [Fact]
    public void OnKeyframeRemoved_Event_Fires()
    {
        // Arrange
        var system = new KeyframeSystem();
        system.AddKeyframe(1.0, 50);

        Keyframe? removedKeyframe = null;
        system.OnKeyframeRemoved += (kf) => removedKeyframe = kf;

        var toRemove = system.Keyframes[0];

        // Act
        system.RemoveKeyframe(toRemove);

        // Assert
        Assert.NotNull(removedKeyframe);
        Assert.Equal(1.0, removedKeyframe.Time);
    }
}
