using VortexCut.Core.Models;
using Xunit;

namespace VortexCut.Tests.Models;

/// <summary>
/// MarkerModel 단위 테스트
/// </summary>
public class MarkerModelTests
{
    [Fact]
    public void MarkerModel_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var marker = new MarkerModel(1, 5000, "Test Marker", MarkerType.Chapter);

        // Assert
        Assert.Equal(1UL, marker.Id);
        Assert.Equal(5000, marker.TimeMs);
        Assert.Equal("Test Marker", marker.Name);
        Assert.Equal(MarkerType.Chapter, marker.Type);
    }

    [Fact]
    public void MarkerModel_DefaultConstructor_UsesDefaults()
    {
        // Arrange & Act
        var marker = new MarkerModel();

        // Assert
        Assert.Equal(0UL, marker.Id);
        Assert.Equal(0, marker.TimeMs);
        Assert.Equal(string.Empty, marker.Name);
        Assert.Equal(MarkerType.Comment, marker.Type);
        Assert.Equal(0xFFFFFF00u, marker.ColorArgb); // Yellow (ARGB)
        Assert.Null(marker.DurationMs);
    }

    [Fact]
    public void IsRegion_WithDuration_ReturnsTrue()
    {
        // Arrange
        var marker = new MarkerModel
        {
            Type = MarkerType.Region,
            DurationMs = 5000
        };

        // Act & Assert
        Assert.True(marker.IsRegion);
    }

    [Fact]
    public void IsRegion_WithoutDuration_ReturnsFalse()
    {
        // Arrange
        var marker = new MarkerModel
        {
            Type = MarkerType.Region,
            DurationMs = null
        };

        // Act & Assert
        Assert.False(marker.IsRegion);
    }

    [Fact]
    public void IsRegion_NotRegionType_ReturnsFalse()
    {
        // Arrange
        var marker = new MarkerModel
        {
            Type = MarkerType.Comment,
            DurationMs = 5000
        };

        // Act & Assert
        Assert.False(marker.IsRegion);
    }

    [Fact]
    public void EndTimeMs_WithDuration_CalculatesCorrectly()
    {
        // Arrange
        var marker = new MarkerModel
        {
            TimeMs = 1000,
            DurationMs = 5000
        };

        // Act
        var endTime = marker.EndTimeMs;

        // Assert
        Assert.Equal(6000, endTime);
    }

    [Fact]
    public void EndTimeMs_WithoutDuration_ReturnsStartTime()
    {
        // Arrange
        var marker = new MarkerModel
        {
            TimeMs = 1000,
            DurationMs = null
        };

        // Act
        var endTime = marker.EndTimeMs;

        // Assert
        Assert.Equal(1000, endTime);
    }

    [Theory]
    [InlineData(MarkerType.Comment, false)]
    [InlineData(MarkerType.Chapter, false)]
    [InlineData(MarkerType.Region, true)]
    public void MarkerType_DifferentTypes_CorrectIsRegionCheck(MarkerType type, bool shouldBeRegion)
    {
        // Arrange
        var marker = new MarkerModel
        {
            Type = type,
            DurationMs = 5000
        };

        // Act & Assert
        Assert.Equal(shouldBeRegion, marker.IsRegion);
    }

    [Fact]
    public void ColorArgb_CustomColor_StoresCorrectly()
    {
        // Arrange
        var marker = new MarkerModel
        {
            ColorArgb = 0xFFFF0000 // Red (ARGB)
        };

        // Act & Assert
        Assert.Equal(0xFFFF0000u, marker.ColorArgb);
    }

    [Fact]
    public void MarkerModel_CommentProperty_StoresCorrectly()
    {
        // Arrange
        var marker = new MarkerModel
        {
            Comment = "This is a test comment"
        };

        // Act & Assert
        Assert.Equal("This is a test comment", marker.Comment);
    }

    [Fact]
    public void EndTimeMs_ZeroDuration_ReturnsStartTime()
    {
        // Arrange
        var marker = new MarkerModel
        {
            TimeMs = 2000,
            DurationMs = 0
        };

        // Act
        var endTime = marker.EndTimeMs;

        // Assert
        Assert.Equal(2000, endTime);
    }

    [Fact]
    public void EndTimeMs_NegativeDuration_CalculatesCorrectly()
    {
        // Arrange (비정상적인 케이스지만 처리 확인)
        var marker = new MarkerModel
        {
            TimeMs = 5000,
            DurationMs = -1000
        };

        // Act
        var endTime = marker.EndTimeMs;

        // Assert
        Assert.Equal(4000, endTime); // 5000 + (-1000)
    }
}
